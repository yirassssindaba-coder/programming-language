Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Public Enum ToolStatus
    Missing = 0
    Available = 1
    [Error] = 2
End Enum

Public NotInheritable Class ToolSpec
    Public Property Name As String = ""
    Public Property Command As String = ""
    Public Property Args As String = ""
    Public Property Category As String = "General"

    ' If set, we will write this string to stdin and then close stdin (useful for tclsh).
    Public Property StdinText As String = Nothing

    ' Timeout for the process (milliseconds)
    Public Property TimeoutMs As Integer = 8000
End Class

Public NotInheritable Class ToolResult
    Public Property Spec As ToolSpec
    Public Property Status As ToolStatus
    Public Property VersionLine As String = "-"
    Public Property FullOutput As String = ""
    Public Property ExitCode As Integer? = Nothing
    Public Property DurationMs As Integer = 0
    Public Property ResolvedPath As String = ""
    Public Property StartedAtUtc As DateTime = DateTime.UtcNow
End Class

Public NotInheritable Class ToolRow
    Public Property Category As String = ""
    Public Property Tool As String = ""
    Public Property Status As String = ""
    Public Property Version As String = ""
    Public Property CommandLine As String = ""
    Public Property Duration As String = ""

    ' Keep reference for details
    Public Property Result As ToolResult = Nothing
End Class
