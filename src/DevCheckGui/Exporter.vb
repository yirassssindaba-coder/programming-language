Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Text.Json

Public NotInheritable Class Exporter

    Public Shared Function BuildTextReport(results As IEnumerable(Of ToolResult), includeDetails As Boolean) As String
        Dim sb As New StringBuilder()

        sb.AppendLine("DevCheck Report")
        sb.AppendLine("Generated: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        sb.AppendLine("Machine: " & Environment.MachineName)
        sb.AppendLine("User: " & Environment.UserName)
        sb.AppendLine(New String("-"c, 60))

        For Each r In results
            Dim statusText As String = StatusToText(r.Status)
            Dim version As String = If(r.VersionLine, "-").Trim()
            If version.Length = 0 Then version = "-"

            sb.AppendLine($"{r.Spec.Name} : {statusText} ({version})")

            If includeDetails Then
                sb.AppendLine($"  Cmd : {r.Spec.Command} {r.Spec.Args}".TrimEnd())
                If Not String.IsNullOrWhiteSpace(r.ResolvedPath) Then sb.AppendLine($"  Path: {r.ResolvedPath}")
                If r.ExitCode.HasValue Then sb.AppendLine($"  Exit: {r.ExitCode.Value}")
                sb.AppendLine($"  Time: {r.DurationMs} ms")

                Dim outText As String = If(r.FullOutput, "").Trim()
                If outText.Length > 0 Then
                    sb.AppendLine("  Output:")
                    For Each line In outText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf)
                        sb.AppendLine("    " & line)
                    Next
                End If

                sb.AppendLine()
            End If
        Next

        Return sb.ToString()
    End Function

    Public Shared Sub SaveAsTxt(path As String, results As IEnumerable(Of ToolResult), includeDetails As Boolean)
        File.WriteAllText(path, BuildTextReport(results, includeDetails), Encoding.UTF8)
    End Sub

    Public Shared Sub SaveAsCsv(path As String, results As IEnumerable(Of ToolResult))
        Dim sb As New StringBuilder()
        sb.AppendLine("Category,Tool,Status,Version,Command,Args,ResolvedPath,ExitCode,DurationMs")

        For Each r In results
            Dim fields As String() = {
                Csv(r.Spec.Category),
                Csv(r.Spec.Name),
                Csv(StatusToText(r.Status)),
                Csv(If(r.VersionLine, "-")),
                Csv(r.Spec.Command),
                Csv(If(r.Spec.Args, "")),
                Csv(If(r.ResolvedPath, "")),
                Csv(If(r.ExitCode.HasValue, r.ExitCode.Value.ToString(), "")),
                Csv(r.DurationMs.ToString())
            }
            sb.AppendLine(String.Join(",", fields))
        Next

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8)
    End Sub

    Public Shared Sub SaveAsJson(path As String, results As IEnumerable(Of ToolResult))
        Dim list As New List(Of Object)()

        For Each r In results
            list.Add(New With {
                .category = r.Spec.Category,
                .name = r.Spec.Name,
                .command = r.Spec.Command,
                .args = If(r.Spec.Args, ""),
                .status = StatusToText(r.Status),
                .versionLine = If(r.VersionLine, "-"),
                .resolvedPath = If(r.ResolvedPath, ""),
                .exitCode = If(r.ExitCode.HasValue, r.ExitCode.Value, CType(Nothing, Integer?)),
                .durationMs = r.DurationMs,
                .startedAtUtc = r.StartedAtUtc
            })
        Next

        Dim opts As New JsonSerializerOptions With {
            .WriteIndented = True
        }

        Dim json As String = JsonSerializer.Serialize(list, opts)
        File.WriteAllText(path, json, Encoding.UTF8)
    End Sub

    Public Shared Function MakeSafeFileName(input As String) As String
        Dim s As String = (If(input, "")).Trim()
        If s.Length = 0 Then s = "devcheck"

        For Each c As Char In System.IO.Path.GetInvalidFileNameChars()
            s = s.Replace(c, "_"c)
        Next

        Return s
    End Function

    Public Shared Function StatusToText(status As ToolStatus) As String
        Select Case status
            Case ToolStatus.Available
                Return "PUNYA"
            Case ToolStatus.Missing
                Return "TIDAK PUNYA"
            Case ToolStatus.Error
                Return "ERROR"
            Case Else
                Return "UNKNOWN"
        End Select
    End Function

    Private Shared Function Csv(value As String) As String
        Dim v As String = If(value, "")

        ' CSV escaping (RFC 4180-ish):
        ' - double embedded quotes
        ' - wrap in quotes if contains comma/quote/newline
        v = v.Replace("""", """""")

        If v.IndexOf(","c) >= 0 OrElse v.IndexOf(""""c) >= 0 OrElse v.Contains(vbCr) OrElse v.Contains(vbLf) Then
            Return """" & v & """"
        End If

        Return v
    End Function

End Class
