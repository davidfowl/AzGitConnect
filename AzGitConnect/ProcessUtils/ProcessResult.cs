// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

internal sealed class ProcessResult
{
    public ProcessResult(int exitCode)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
