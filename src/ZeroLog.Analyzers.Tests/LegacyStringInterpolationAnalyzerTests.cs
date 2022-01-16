﻿using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace ZeroLog.Analyzers.Tests;

[TestFixture]
public class LegacyStringInterpolationAnalyzerTests
{
    [Test]
    public Task should_not_report_non_allocating_interpolation()
    {
        var test = new Test
        {
            LanguageVersion = LanguageVersion.CSharp10,
            Source = @"
class C
{
    void M(ZeroLog.Log log)
        => log.Info({|#0:$""""|});
}
"
        };

        return test.RunAsync();
    }

    [Test]
    public Task should_report_allocating_interpolation_on_log()
    {
        var test = new Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            Source = @"
class C
{
    void M(ZeroLog.Log log)
        => log.Info({|#0:$""""|});
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(LegacyStringInterpolationAnalyzer.AllocatingStringInterpolationDiagnostic).WithLocation(0)
            }
        };

        return test.RunAsync();
    }

    [Test]
    public Task should_report_allocating_interpolation_on_log_message()
    {
        var test = new Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            Source = @"
class C
{
    void M(ZeroLog.LogMessage message)
        => message.Append({|#0:$""""|});
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(LegacyStringInterpolationAnalyzer.AllocatingStringInterpolationDiagnostic).WithLocation(0)
            }
        };

        return test.RunAsync();
    }

    private class Test : ZeroLogAnalyzerTest<LegacyStringInterpolationAnalyzer>
    {
    }
}
