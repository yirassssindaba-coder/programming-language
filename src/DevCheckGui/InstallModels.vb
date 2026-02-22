Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Public Enum InstallProvider
    None = 0
    Winget = 1
    Chocolatey = 2
    Scoop = 3
    Npm = 4
End Enum

Public NotInheritable Class InstallStep
    Public Property Provider As InstallProvider = InstallProvider.None
    Public Property Exe As String = ""
    Public Property Args As String = ""
    Public Property RequiresAdmin As Boolean = False
    Public Property TimeoutMs As Integer = 600000 ' 10 minutes
End Class

Public NotInheritable Class InstallPlan
    Public Property DisplayName As String = ""
    Public Property Steps As New List(Of InstallStep)()

    ' Extra PATH directories to ensure (User + Process). Each entry may contain env vars.
    Public Property EnsurePathDirs As New List(Of String)()

    Public Function IsEmpty() As Boolean
        Return Steps Is Nothing OrElse Steps.Count = 0
    End Function
End Class

Public NotInheritable Class InstallResult
    Public Property Success As Boolean = False
    Public Property ProviderUsed As InstallProvider = InstallProvider.None
    Public Property ExitCode As Integer? = Nothing
    Public Property Output As String = ""
    Public Property ErrorMessage As String = ""
End Class
