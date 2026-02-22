Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms

Module Program

    ' NOTE:
    ' Some WinForms startup exceptions can cause the process to exit without showing any UI.
    ' To make failures visible, we log to %TEMP% and show a MessageBox.
    Private ReadOnly LogPath As String = Path.Combine(Path.GetTempPath(), "DevCheckGui.log")

    Private Sub LogException(context As String, ex As Exception)
        Try
            Dim sb As New StringBuilder()
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}")
            sb.AppendLine(ex.ToString())
            sb.AppendLine(New String("-"c, 80))
            File.AppendAllText(LogPath, sb.ToString())
        Catch
            ' ignore logging failures
        End Try
    End Sub

    Private Sub ShowFatal(context As String, ex As Exception)
        LogException(context, ex)
        Try
            MessageBox.Show(
                $"{context}{vbCrLf}{vbCrLf}{ex}{vbCrLf}{vbCrLf}Log: {LogPath}",
                "DevCheck — Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )
        Catch
            ' ignore UI failures
        End Try
    End Sub

    Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        ShowFatal("Unhandled UI exception.", e.Exception)
    End Sub

    Private Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex As Exception = TryCast(e.ExceptionObject, Exception)
        If ex Is Nothing Then
            ex = New Exception("Unhandled exception (non-Exception object).")
        End If
        ShowFatal("Unhandled non-UI exception.", ex)
    End Sub

    <STAThread>
    Sub Main()
        ' Make startup failures visible instead of silently exiting.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException

        ' Modern DPI handling (recommended for WinForms on .NET 6+)
        Try
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Catch
            ' ignore on older environments
        End Try

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Try
            Application.Run(New MainForm())
        Catch ex As Exception
            ' If MainForm fails to construct or run, show an error window so user sees *something*.
            ShowFatal("Failed to start the application.", ex)
            Application.Run(New ErrorForm(ex, LogPath))
        End Try
    End Sub

End Module
