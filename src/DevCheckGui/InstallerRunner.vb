Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Public NotInheritable Class InstallerRunner

    Public Shared Function HasProvider(installStep As InstallStep) As Boolean
        Dim resolved As String = CommandRunner.ResolveExecutable(installStep.Exe)
        Return Not String.IsNullOrWhiteSpace(resolved)
    End Function

    Public Shared Async Function InstallAsync(spec As ToolSpec, token As CancellationToken, Optional forceAdmin As Boolean = False) As Task(Of InstallResult)
        Dim plan As InstallPlan = InstallerCatalog.GetPlan(spec)
        Dim result As New InstallResult()

        If plan Is Nothing OrElse plan.IsEmpty() Then
            result.Success = False
            result.ErrorMessage = "No installer mapping for this tool."
            Return result
        End If

        ' If Scoop is a viable installer for this tool but Scoop is not installed,
        ' try to bootstrap Scoop (user-scoped, no admin) so we can use it.
        Dim bootstrapLog As String = ""
        Dim wantsScoop As Boolean = False
        For Each s In plan.Steps
            If s.Provider = InstallProvider.Scoop Then
                wantsScoop = True
                Exit For
            End If
        Next

        If wantsScoop AndAlso String.IsNullOrWhiteSpace(CommandRunner.ResolveExecutable("scoop")) Then
            Try
                bootstrapLog = Await BootstrapScoopAsync(token)

                ' Make Scoop available in the current process immediately
                Try
                    Dim up As String = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    Dim shims As String = Path.Combine(up, "scoop", "shims")
                    PathHelper.EnsureUserAndProcessPathContains(shims)
                Catch
                    ' ignore
                End Try
            Catch ex As Exception
                bootstrapLog = "[BOOTSTRAP] Scoop bootstrap failed: " & ex.Message
            End Try
        End If

        ' Pick the first available provider step
        Dim chosen As InstallStep = Nothing
        For Each s In plan.Steps
            If HasProvider(s) Then
                chosen = s
                Exit For
            End If
        Next

        If chosen Is Nothing Then
            result.Success = False
            result.ErrorMessage = "No supported installer found on this machine (need winget / choco / scoop / npm)."
            result.Output = bootstrapLog & If(bootstrapLog.Length > 0, Environment.NewLine & Environment.NewLine, "") & InstallerCatalog.BuildHumanHint(spec)
            Return result
        End If

        Dim runAsAdmin As Boolean = forceAdmin OrElse chosen.RequiresAdmin

        Dim execRes As InstallResult = Await RunStepAsync(chosen, token, runAsAdmin)
        execRes.ProviderUsed = chosen.Provider

        ' Ensure PATH dirs if install succeeded (or even if it didn't, best effort for npm/cargo paths)
        If plan.EnsurePathDirs IsNot Nothing Then
            For Each d In plan.EnsurePathDirs
                Try
                    PathHelper.EnsureUserAndProcessPathContains(d)
                Catch
                End Try
            Next
        End If

        If Not String.IsNullOrWhiteSpace(bootstrapLog) Then
            execRes.Output = bootstrapLog & Environment.NewLine & Environment.NewLine & If(execRes.Output, "")
        End If

        Return execRes
    End Function

    Private Shared Async Function BootstrapScoopAsync(token As CancellationToken) As Task(Of String)
        Dim psCmd As String = "Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; iwr -useb get.scoop.sh | iex"
        ' psCmd contains no quotes; wrap it for -Command
        Dim args As String = "-NoProfile -ExecutionPolicy Bypass -Command """ & psCmd & """"

        Dim spec As New ToolSpec With {
            .Name = "Scoop bootstrap",
            .Category = "System",
            .Command = "powershell",
            .Args = args,
            .TimeoutMs = 300000
        }

        Dim r As ToolResult = Await CommandRunner.RunAsync(spec, token)
        Return "[BOOTSTRAP] Installing Scoop (CurrentUser)..." & Environment.NewLine & r.FullOutput
    End Function

    Private Shared Async Function RunStepAsync(installStep As InstallStep, token As CancellationToken, runAsAdmin As Boolean) As Task(Of InstallResult)
        Dim res As New InstallResult With {.Success = False, .ProviderUsed = installStep.Provider}

        Dim exeResolved As String = CommandRunner.ResolveExecutable(installStep.Exe)
        If String.IsNullOrWhiteSpace(exeResolved) Then
            res.ErrorMessage = $"Installer executable not found: {installStep.Exe}"
            Return res
        End If

        If runAsAdmin Then
            ' Admin run: cannot capture output reliably (UseShellExecute=True)
            Dim psi As New ProcessStartInfo() With {
                .FileName = exeResolved,
                .Arguments = installStep.Args,
                .UseShellExecute = True,
                .Verb = "runas"
            }

            Try
                Using p As Process = Process.Start(psi)
                    If p Is Nothing Then
                        res.ErrorMessage = "Failed to start installer process."
                        Return res
                    End If

                    Await Task.Run(Sub() p.WaitForExit(), token)
                    res.ExitCode = p.ExitCode
                    res.Success = (p.ExitCode = 0)
                    res.Output = $"(Admin run) {installStep.Exe} {installStep.Args}{Environment.NewLine}ExitCode={p.ExitCode}{Environment.NewLine}Note: output is not captured in admin mode."
                    If Not res.Success Then
                        res.ErrorMessage = "Installer exited with non-zero exit code."
                    End If
                    Return res
                End Using
            Catch ex As Exception
                res.ErrorMessage = ex.Message
                res.Output = ex.ToString()
                Return res
            End Try
        End If

        ' Non-admin run: capture output
        Dim psi2 As New ProcessStartInfo() With {
            .FileName = exeResolved,
            .Arguments = installStep.Args,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .UseShellExecute = False,
            .CreateNoWindow = True
        }

        Try
            Using p As New Process()
                p.StartInfo = psi2

                If Not p.Start() Then
                    res.ErrorMessage = "Failed to start installer process."
                    Return res
                End If

                Dim stdoutTask As Task(Of String) = p.StandardOutput.ReadToEndAsync()
                Dim stderrTask As Task(Of String) = p.StandardError.ReadToEndAsync()

                Dim timeoutMs As Integer = Math.Max(10000, installStep.TimeoutMs)
                Dim exitTask As Task = p.WaitForExitAsync(token)
                Dim delayTask As Task = Task.Delay(timeoutMs, token)

                Dim finished As Task = Await Task.WhenAny(exitTask, delayTask)
                If finished Is delayTask Then
                    Try
                        p.Kill(True)
                    Catch
                    End Try

                    Dim so As String = ""
                    Dim se As String = ""
                    Try : so = Await stdoutTask : Catch : End Try
                    Try : se = Await stderrTask : Catch : End Try

                    res.ExitCode = Nothing
                    res.Success = False
                    res.ErrorMessage = "Installer TIMEOUT"
                    res.Output = Combine(so, se) & If((so & se).Trim().Length > 0, Environment.NewLine, "") & $"[TIMEOUT] {timeoutMs} ms"
                    Return res
                End If

                Dim stdout As String = Await stdoutTask
                Dim stderr As String = Await stderrTask

                res.ExitCode = p.ExitCode
                res.Output = Combine(stdout, stderr)
                res.Success = (p.ExitCode = 0)
                If Not res.Success Then
                    res.ErrorMessage = "Installer exited with non-zero exit code."
                End If

                Return res
            End Using
        Catch ex As Exception
            res.ErrorMessage = ex.Message
            res.Output = ex.ToString()
            Return res
        End Try
    End Function

    Private Shared Function Combine(a As String, b As String) As String
        Dim x As String = If(a, "").Trim()
        Dim y As String = If(b, "").Trim()
        If x.Length = 0 Then Return y
        If y.Length = 0 Then Return x
        Return (x & Environment.NewLine & y).Trim()
    End Function

End Class
