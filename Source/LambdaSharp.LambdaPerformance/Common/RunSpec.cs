namespace LambdaSharp.LambdaPerformance.Common;

public record RunSpec(
    string? Project,
    string? Payload,
    string? Handler,
    string? Runtime,
    string? Architecture,
    string? ZipFile,
    long ZipSize,
    string? Tiered,
    string? Ready2Run,
    int MemorySize
);
