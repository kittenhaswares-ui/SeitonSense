using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum SamuraiMovementInputPath : byte
{
    Pressed,
    Down,
    Held,
    ControlState,
    Autorun,
}

internal readonly record struct SamuraiMovementInputDiagnostics(
    long DigitalMovementReads,
    long ControlMovementReads,
    long AutorunReads,
    long SuppressedDigitalReads,
    long SuppressedControlReads,
    long SuppressedAutorunReads,
    long OwnershipReadFailures);

internal delegate bool SamuraiMovementNativeReader(
    nint inputAddress,
    uint inputCode,
    SamuraiMovementInputPath path);

/// <summary>
/// Shared native-input boundary, also executable with a fake native reader.
/// It changes only a true movement result while an exact owned cast is active.
/// </summary>
internal sealed class SamuraiCastMovementInputBoundary(
    SamuraiMovementNativeReader readNative,
    Func<bool> shouldSuppress)
{
    [ThreadStatic]
    private static int ownershipReadDepth;

    private long digitalReads;
    private long controlReads;
    private long autorunReads;
    private long suppressedDigital;
    private long suppressedControl;
    private long suppressedAutorun;
    private long failures;

    internal SamuraiMovementInputDiagnostics Diagnostics => new(
        Interlocked.Read(ref digitalReads),
        Interlocked.Read(ref controlReads),
        Interlocked.Read(ref autorunReads),
        Interlocked.Read(ref suppressedDigital),
        Interlocked.Read(ref suppressedControl),
        Interlocked.Read(ref suppressedAutorun),
        Interlocked.Read(ref failures));

    internal bool Read(nint inputAddress, uint inputCode, SamuraiMovementInputPath path)
    {
        var nativeResult = readNative(inputAddress, inputCode, path);
        if (!nativeResult || ownershipReadDepth != 0 || !IsMovement(path, inputCode))
            return nativeResult;

        switch (path)
        {
            case SamuraiMovementInputPath.ControlState: Interlocked.Increment(ref controlReads); break;
            case SamuraiMovementInputPath.Autorun: Interlocked.Increment(ref autorunReads); break;
            default: Interlocked.Increment(ref digitalReads); break;
        }

        try
        {
            ownershipReadDepth++;
            if (!shouldSuppress()) return nativeResult;
        }
        catch
        {
            Interlocked.Increment(ref failures);
            return nativeResult;
        }
        finally
        {
            ownershipReadDepth--;
        }

        switch (path)
        {
            case SamuraiMovementInputPath.ControlState: Interlocked.Increment(ref suppressedControl); break;
            case SamuraiMovementInputPath.Autorun: Interlocked.Increment(ref suppressedAutorun); break;
            default: Interlocked.Increment(ref suppressedDigital); break;
        }
        return false;
    }

    private static bool IsMovement(SamuraiMovementInputPath path, uint inputCode) => path switch
    {
        SamuraiMovementInputPath.Pressed or SamuraiMovementInputPath.Down or SamuraiMovementInputPath.Held =>
            SamuraiOgiCastProtectionRules.IsMovementInputId(inputCode),
        SamuraiMovementInputPath.ControlState =>
            SamuraiOgiCastProtectionRules.IsMovementControlCode(inputCode),
        SamuraiMovementInputPath.Autorun => true,
        _ => false,
    };
}
