namespace KeyPulse.Core;

public sealed record ScreenImageCropSettings(
    double X,
    double Y,
    double Width,
    double Height,
    double TargetAspect,
    string LayoutSignature,
    int Version = 1)
{
    public bool IsValid =>
        Version == 1 &&
        X >= 0 && Y >= 0 && Width > 0 && Height > 0 &&
        X + Width <= 1.000001 && Y + Height <= 1.000001 &&
        TargetAspect > 0 && !string.IsNullOrWhiteSpace(LayoutSignature);

    public bool Matches(string layoutSignature, double targetAspect) =>
        IsValid &&
        string.Equals(LayoutSignature, layoutSignature, StringComparison.Ordinal) &&
        Math.Abs(TargetAspect - targetAspect) <= 0.0001;
}
