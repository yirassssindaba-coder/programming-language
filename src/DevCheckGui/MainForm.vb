Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class MainForm
    Inherits Form

    Private ReadOnly _tools As List(Of ToolSpec) = ToolCatalog.GetDefaultTools()
    Private ReadOnly _results As New List(Of ToolResult)()
    Private ReadOnly _bindingSource As New BindingSource()

    Private _cts As CancellationTokenSource = Nothing
    Private _theme As AppTheme = AppTheme.Light


    Private _suppressCategoryEvents As Boolean = False
    ' UI
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cmbStatus As New ComboBox()
    Private ReadOnly clbCategories As New CheckedListBox()

    Private ReadOnly btnScan As New Button()
    Private ReadOnly btnStop As New Button()
    Private ReadOnly btnExport As New Button()
    Private ReadOnly btnCopy As New Button()
    Private ReadOnly btnInstallMissing As New Button()

    Private ReadOnly chkAutoInstall As New CheckBox()
    Private ReadOnly chkIncludeDetails As New CheckBox()
    Private ReadOnly chkAutoScan As New CheckBox()
    Private ReadOnly btnTheme As New CheckBox()

    Private ReadOnly lblSummary As New Label()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly progress As New ProgressBar()

    Private ReadOnly txtDetails As New TextBox()
    Private ReadOnly lblDetailTitle As New Label()
    Private ReadOnly btnInstallSelected As New Button()
    Private ReadOnly btnEnsurePath As New Button()

    Public Sub New()
        Text = "DevCheck — Toolchain Version Checker"
        StartPosition = FormStartPosition.CenterScreen
        MinimumSize = New Size(1100, 680)
        Font = New Font("Segoe UI", 10.0F)

        InitializeUi()
        PopulateFilters()
        RefreshGrid()

        ApplyTheme(_theme)
    End Sub

    Private Sub InitializeUi()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 3,
            .ColumnCount = 1,
            .Padding = New Padding(14)
        }
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Controls.Add(root)

        ' -----------------------
        ' Header
        ' -----------------------
        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .RowCount = 2,
            .ColumnCount = 2,
            .AutoSize = True
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        Dim titleBlock As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1, .AutoSize = True}
        Dim lblTitle As New Label() With {
            .Text = "DevCheck",
            .AutoSize = True,
            .Font = New Font("Segoe UI Semibold", 20.0F)
        }
        Dim lblSub As New Label() With {
            .Text = "Cek versi tools developer (PATH) — async, cepat, dan bisa export.",
            .AutoSize = True
        }
        titleBlock.Controls.Add(lblTitle, 0, 0)
        titleBlock.Controls.Add(lblSub, 0, 1)

        Dim toolbar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .AutoSize = True,
            .WrapContents = False,
            .Padding = New Padding(0, 6, 0, 0)
        }

        btnScan.Text = "Scan"
        btnStop.Text = "Stop"
        btnExport.Text = "Export"
        btnCopy.Text = "Copy"
        btnInstallMissing.Text = "Fix Missing/Error"

        btnStop.Enabled = False

        AddHandler btnScan.Click, AddressOf OnScan
        AddHandler btnStop.Click, AddressOf OnStop
        AddHandler btnExport.Click, AddressOf OnExport
        AddHandler btnCopy.Click, AddressOf OnCopy
        AddHandler btnInstallMissing.Click, AddressOf OnInstallMissing

        chkAutoInstall.Text = "Auto fix missing/error"
        chkAutoInstall.AutoSize = True

        chkIncludeDetails.Text = "Include details"
        chkIncludeDetails.AutoSize = True

        chkAutoScan.Text = "Auto scan on start"
        chkAutoScan.AutoSize = True

        btnTheme.Appearance = Appearance.Button
        btnTheme.AutoSize = True
        btnTheme.Text = "Dark"
        btnTheme.TextAlign = ContentAlignment.MiddleCenter
        AddHandler btnTheme.CheckedChanged, AddressOf OnThemeToggle

        toolbar.Controls.Add(btnScan)
        toolbar.Controls.Add(btnStop)
        toolbar.Controls.Add(btnExport)
        toolbar.Controls.Add(btnCopy)
        toolbar.Controls.Add(btnInstallMissing)
        toolbar.Controls.Add(btnTheme)
        toolbar.Controls.Add(chkAutoInstall)
        toolbar.Controls.Add(chkIncludeDetails)
        toolbar.Controls.Add(chkAutoScan)

        header.Controls.Add(titleBlock, 0, 0)
        header.SetRowSpan(titleBlock, 2)
        header.Controls.Add(toolbar, 1, 0)

        ' quick system info
        Dim sysInfo As New Label() With {
            .AutoSize = True,
            .TextAlign = ContentAlignment.MiddleRight
        }
        sysInfo.Text =
            $"{Environment.OSVersion.VersionString} | {If(Environment.Is64BitOperatingSystem, "x64", "x86")} | {Environment.MachineName}"
        header.Controls.Add(sysInfo, 1, 1)

        root.Controls.Add(header, 0, 0)

        ' -----------------------
        ' Main (left filters + right results)
        ' -----------------------
        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 1,
            .ColumnCount = 2
        }
        main.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        main.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 70.0F))
        root.Controls.Add(main, 0, 1)

        ' Left panel (filters + summary)
        Dim left As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 4,
            .ColumnCount = 1
        }
        left.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        left.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        left.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        main.Controls.Add(left, 0, 0)

        Dim grpFilters As New GroupBox() With {.Text = "Filters", .Dock = DockStyle.Top, .AutoSize = True}
        Dim filters As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 4, .ColumnCount = 2, .AutoSize = True, .Padding = New Padding(10)}
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim lblSearch As New Label() With {.Text = "Search", .AutoSize = True}
        txtSearch.Dock = DockStyle.Fill
        txtSearch.PlaceholderText = "tool / version / command…"
        AddHandler txtSearch.TextChanged, Sub() RefreshGrid()

        Dim lblStatusFilter As New Label() With {.Text = "Status", .AutoSize = True}
        cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cmbStatus.Dock = DockStyle.Fill
        AddHandler cmbStatus.SelectedIndexChanged, Sub() RefreshGrid()

        Dim lblCat As New Label() With {.Text = "Category", .AutoSize = True}

        clbCategories.Dock = DockStyle.Fill
        clbCategories.CheckOnClick = True
        clbCategories.IntegralHeight = False
        clbCategories.Height = 220
        AddHandler clbCategories.ItemCheck, AddressOf OnCategoryItemCheck

        Dim btnCatAll As New Button() With {.Text = "All", .AutoSize = True}
        Dim btnCatNone As New Button() With {.Text = "None", .AutoSize = True}
        AddHandler btnCatAll.Click, Sub() SetAllCategories(True)
        AddHandler btnCatNone.Click, Sub() SetAllCategories(False)

        Dim catBtns As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True}
        catBtns.Controls.Add(btnCatAll)
        catBtns.Controls.Add(btnCatNone)

        filters.Controls.Add(lblSearch, 0, 0)
        filters.Controls.Add(txtSearch, 1, 0)
        filters.Controls.Add(lblStatusFilter, 0, 1)
        filters.Controls.Add(cmbStatus, 1, 1)
        filters.Controls.Add(lblCat, 0, 2)
        filters.Controls.Add(clbCategories, 1, 2)
        filters.Controls.Add(New Label() With {.Text = "", .AutoSize = True}, 0, 3)
        filters.Controls.Add(catBtns, 1, 3)

        grpFilters.Controls.Add(filters)
        left.Controls.Add(grpFilters, 0, 0)

        Dim grpSummary As New GroupBox() With {.Text = "Summary", .Dock = DockStyle.Top, .AutoSize = True}
        Dim summaryPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1, .AutoSize = True, .Padding = New Padding(10)}
        lblSummary.AutoSize = True
        lblSummary.Text = "Ready."
        summaryPanel.Controls.Add(lblSummary, 0, 0)

        Dim tip As New Label() With {.AutoSize = True, .Text = "Tip: klik baris untuk lihat detail output."}
        summaryPanel.Controls.Add(tip, 0, 1)
        grpSummary.Controls.Add(summaryPanel)
        left.Controls.Add(grpSummary, 0, 1)

        Dim grpHelp As New GroupBox() With {.Text = "Export", .Dock = DockStyle.Fill}
        Dim helpPanel As New Label() With {
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .TextAlign = ContentAlignment.TopLeft,
            .Padding = New Padding(10),
            .Text =
                "Export format:" & vbCrLf &
                "• TXT: report readable" & vbCrLf &
                "• JSON: untuk automation" & vbCrLf &
                "• CSV: buat Excel" & vbCrLf & vbCrLf &
                "Copy = salin report (TXT) ke clipboard."
        }
        grpHelp.Controls.Add(helpPanel)
        left.Controls.Add(grpHelp, 0, 2)

        ' Right panel (grid + details)
        Dim right As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1}
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 62.0F))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 38.0F))
        main.Controls.Add(right, 1, 0)

        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoGenerateColumns = False
        grid.RowHeadersVisible = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

        EnableDoubleBuffering(grid)

        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "Category", .DataPropertyName = "Category", .FillWeight = 18.0F})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "Tool", .DataPropertyName = "Tool", .FillWeight = 25.0F})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "Status", .DataPropertyName = "Status", .FillWeight = 18.0F})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "Version", .DataPropertyName = "Version", .FillWeight = 39.0F})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "Time", .DataPropertyName = "Duration", .FillWeight = 10.0F})

        AddHandler grid.SelectionChanged, AddressOf OnGridSelectionChanged
        AddHandler grid.CellFormatting, AddressOf OnGridCellFormatting

        right.Controls.Add(grid, 0, 0)

        Dim detailsBox As New GroupBox() With {.Text = "Details", .Dock = DockStyle.Fill}
        Dim detailsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 3, .ColumnCount = 1, .Padding = New Padding(10)}
        detailsLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        detailsLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        detailsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        lblDetailTitle.AutoSize = True
        lblDetailTitle.Text = "Select a row…"

        txtDetails.Dock = DockStyle.Fill
        txtDetails.Multiline = True
        txtDetails.ScrollBars = ScrollBars.Vertical
        txtDetails.ReadOnly = True
        txtDetails.Font = New Font("Consolas", 10.0F)

        Dim actions As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = False}

        btnInstallSelected.Text = "Fix Selected"
        btnInstallSelected.AutoSize = True
        btnInstallSelected.Enabled = False
        AddHandler btnInstallSelected.Click, AddressOf OnInstallSelected

        btnEnsurePath.Text = "Ensure PATH"
        btnEnsurePath.AutoSize = True
        btnEnsurePath.Enabled = False
        AddHandler btnEnsurePath.Click, AddressOf OnEnsurePathSelected

        actions.Controls.Add(btnInstallSelected)
        actions.Controls.Add(btnEnsurePath)

        detailsLayout.Controls.Add(lblDetailTitle, 0, 0)
        detailsLayout.Controls.Add(actions, 0, 1)
        detailsLayout.Controls.Add(txtDetails, 0, 2)
        detailsBox.Controls.Add(detailsLayout)

        right.Controls.Add(detailsBox, 0, 1)

        ' -----------------------
        ' Footer
        ' -----------------------
        Dim footer As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .RowCount = 1, .ColumnCount = 2, .AutoSize = True}
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        lblStatus.AutoSize = True
        lblStatus.Text = "Idle."

        progress.Width = 280
        progress.Minimum = 0
        progress.Value = 0

        footer.Controls.Add(lblStatus, 0, 0)
        footer.Controls.Add(progress, 1, 0)

        root.Controls.Add(footer, 0, 2)

        ' Style buttons consistently (theme manager will override colors later)
        For Each b In New Button() {btnScan, btnStop, btnExport, btnCopy}
            b.AutoSize = True
        Next
    End Sub

        Private Sub PopulateFilters()
        cmbStatus.Items.Clear()
        cmbStatus.Items.Add("All")
        cmbStatus.Items.Add("PUNYA")
        cmbStatus.Items.Add("TIDAK PUNYA")
        cmbStatus.Items.Add("ERROR")
        cmbStatus.SelectedIndex = 0

        Dim cats As List(Of String) = ToolCatalog.GetAllCategories(_tools)

        ' Prevent ItemCheck from firing refresh while we are still constructing the control
        _suppressCategoryEvents = True
        RemoveHandler clbCategories.ItemCheck, AddressOf OnCategoryItemCheck
        Try
            clbCategories.BeginUpdate()
            clbCategories.Items.Clear()
            For Each c In cats
                clbCategories.Items.Add(c, True)
            Next
        Finally
            clbCategories.EndUpdate()
            AddHandler clbCategories.ItemCheck, AddressOf OnCategoryItemCheck
            _suppressCategoryEvents = False
        End Try
    End Sub

        Private Sub SetAllCategories(value As Boolean)
        _suppressCategoryEvents = True
        Try
            clbCategories.BeginUpdate()
            For i As Integer = 0 To clbCategories.Items.Count - 1
                clbCategories.SetItemChecked(i, value)
            Next
        Finally
            clbCategories.EndUpdate()
            _suppressCategoryEvents = False
        End Try

        RefreshGrid()
    End Sub

        Private Sub OnCategoryItemCheck(sender As Object, e As ItemCheckEventArgs)
        If _suppressCategoryEvents Then Return

        ' ItemCheck fires before the check state changes, so refresh after UI updates.
        If Me.IsHandleCreated Then
            Me.BeginInvoke(New Action(Sub() RefreshGrid()))
        Else
            ' During startup the handle may not exist yet.
        End If
    End Sub

    Private Function SelectedCategories() As HashSet(Of String)
        Dim setCat As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each item In clbCategories.CheckedItems
            Dim s As String = item.ToString()
            If Not String.IsNullOrWhiteSpace(s) Then setCat.Add(s.Trim())
        Next
        Return setCat
    End Function

    Private Function CurrentViewResults() As List(Of ToolResult)
        Dim catSet As HashSet(Of String) = SelectedCategories()
        Dim q As String = (If(txtSearch.Text, "")).Trim()
        Dim statusFilter As String = If(cmbStatus.SelectedItem, "All").ToString()

        Dim list As IEnumerable(Of ToolResult) = _results

        ' category filter
        If catSet.Count > 0 Then
            list = list.Where(Function(r) catSet.Contains(r.Spec.Category))
        Else
            ' none selected => show nothing
            list = list.Where(Function(r) False)
        End If

        ' status filter
        If statusFilter <> "All" Then
            list = list.Where(Function(r) Exporter.StatusToText(r.Status).Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
        End If

        ' search filter
        If q.Length > 0 Then
            list = list.Where(Function(r)
                                  Dim hay As String =
                                      (r.Spec.Name & " " & r.Spec.Category & " " & r.Spec.Command & " " & r.Spec.Args & " " & If(r.VersionLine, "")).ToLowerInvariant()
                                  Return hay.Contains(q.ToLowerInvariant())
                              End Function)
        End If

        ' default sort: category, tool name
        list = list.OrderBy(Function(r) r.Spec.Category).ThenBy(Function(r) r.Spec.Name)

        Return list.ToList()
    End Function

    Private Sub RefreshGrid()
        Dim view As List(Of ToolResult) = CurrentViewResults()

        Dim rows As New List(Of ToolRow)()
        For Each r In view
            rows.Add(New ToolRow With {
                .Category = r.Spec.Category,
                .Tool = r.Spec.Name,
                .Status = StatusPretty(r.Status),
                .Version = If(r.VersionLine, "-"),
                .CommandLine = (r.Spec.Command & " " & If(r.Spec.Args, "")).Trim(),
                .Duration = r.DurationMs & " ms",
                .Result = r
            })
        Next

        Dim bl As New BindingList(Of ToolRow)(rows)
        _bindingSource.DataSource = bl
        grid.DataSource = _bindingSource

        UpdateSummary()
    End Sub

    Private Sub UpdateSummary()
        Dim total As Integer = _results.Count
        ' NOTE: List(Of T).Count is a property, so calling Count(predicate) would be parsed as indexing the property.
        ' Use LINQ Where(...).Count() instead.
        Dim ok As Integer = _results.Where(Function(r) r.Status = ToolStatus.Available).Count()
        Dim miss As Integer = _results.Where(Function(r) r.Status = ToolStatus.Missing).Count()
        Dim err As Integer = _results.Where(Function(r) r.Status = ToolStatus.Error).Count()

        Dim viewCount As Integer = CurrentViewResults().Count

        lblSummary.Text = $"Total scanned: {total} | ✅ PUNYA: {ok} | ❌ TIDAK PUNYA: {miss} | ⚠️ ERROR: {err} | Showing: {viewCount}"
    End Sub

    Private Shared Function StatusPretty(status As ToolStatus) As String
        Select Case status
            Case ToolStatus.Available
                Return "✅ PUNYA"
            Case ToolStatus.Missing
                Return "❌ TIDAK PUNYA"
            Case ToolStatus.Error
                Return "⚠️ ERROR"
            Case Else
                Return "?"
        End Select
    End Function

    Private Sub OnGridSelectionChanged(sender As Object, e As EventArgs)
        If grid.SelectedRows.Count = 0 Then
            lblDetailTitle.Text = "Select a row…"
            txtDetails.Text = ""
            Return
        End If

        Dim row As DataGridViewRow = grid.SelectedRows(0)
        Dim view As ToolRow = TryCast(row.DataBoundItem, ToolRow)
        If view Is Nothing OrElse view.Result Is Nothing Then
            lblDetailTitle.Text = "Select a row…"
            txtDetails.Text = ""
            Return
        End If

        ShowDetails(view.Result)
    End Sub

    Private Sub ShowDetails(r As ToolResult)
        Dim sb As New StringBuilder()
        sb.AppendLine($"{r.Spec.Name}  [{r.Spec.Category}]")
        sb.AppendLine($"Status      : {Exporter.StatusToText(r.Status)}")
        sb.AppendLine($"Command     : {r.Spec.Command} {If(r.Spec.Args, "")}".TrimEnd())
        If Not String.IsNullOrWhiteSpace(r.ResolvedPath) Then sb.AppendLine($"Resolved    : {r.ResolvedPath}")
        If r.ExitCode.HasValue Then sb.AppendLine($"ExitCode    : {r.ExitCode.Value}")
        sb.AppendLine($"Time        : {r.DurationMs} ms")
        sb.AppendLine(New String("-"c, 50))
        sb.AppendLine(If(r.FullOutput, ""))

        If r.Status = ToolStatus.Missing OrElse r.Status = ToolStatus.Error Then
            sb.AppendLine()
            sb.AppendLine("---- INSTALL ----")
            sb.AppendLine(InstallerCatalog.BuildHumanHint(r.Spec))
        End If

        lblDetailTitle.Text = $"{r.Spec.Name} — {StatusPretty(r.Status)}"
        txtDetails.Text = sb.ToString()

        btnInstallSelected.Enabled = (r.Status = ToolStatus.Missing OrElse r.Status = ToolStatus.Error)
        btnEnsurePath.Enabled = Not String.IsNullOrWhiteSpace(r.ResolvedPath)
    End Sub

    Private Sub OnGridCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row As DataGridViewRow = grid.Rows(e.RowIndex)
        Dim item As ToolRow = TryCast(row.DataBoundItem, ToolRow)
        If item Is Nothing OrElse item.Result Is Nothing Then Return

        ' Simple status-based row tint
        If item.Result.Status = ToolStatus.Missing Then
            row.DefaultCellStyle.ForeColor = Color.FromArgb(239, 68, 68) ' red-ish
        ElseIf item.Result.Status = ToolStatus.Error Then
            row.DefaultCellStyle.ForeColor = Color.FromArgb(245, 158, 11) ' amber-ish
        Else
            row.DefaultCellStyle.ForeColor = Color.Empty
        End If
    End Sub

    Private Async Sub OnScan(sender As Object, e As EventArgs)
        If _cts IsNot Nothing Then Return

        Dim cats As HashSet(Of String) = SelectedCategories()
        Dim toScan As List(Of ToolSpec) = _tools.Where(Function(t) cats.Contains(t.Category)).ToList()

        If toScan.Count = 0 Then
            MessageBox.Show("Pilih minimal 1 category untuk scan.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        _results.Clear()
        RefreshGrid()

        progress.Value = 0
        progress.Maximum = toScan.Count

        btnScan.Enabled = False
        btnStop.Enabled = True
        btnExport.Enabled = False
        btnCopy.Enabled = False
        clbCategories.Enabled = False
        cmbStatus.Enabled = False
        txtSearch.Enabled = False
        chkAutoInstall.Enabled = False

        lblStatus.Text = "Scanning…"

        _cts = New CancellationTokenSource()
        Dim token As CancellationToken = _cts.Token

        Try
            For i As Integer = 0 To toScan.Count - 1
                If token.IsCancellationRequested Then Exit For

                Dim spec As ToolSpec = toScan(i)
                lblStatus.Text = $"Checking {spec.Name} ({i + 1}/{toScan.Count})…"

                Dim result As ToolResult = Await CommandRunner.RunAsync(spec, token)

                If chkAutoInstall.Checked AndAlso (result.Status = ToolStatus.Missing OrElse result.Status = ToolStatus.Error) AndAlso Not token.IsCancellationRequested Then
                    lblStatus.Text = $"Fixing {spec.Name}…"
                    Dim fixed As ToolResult = Await AutoFixer.FixAsync(spec, result, token)
                    result = fixed
                End If

                _results.Add(result)

                progress.Value = Math.Min(progress.Maximum, i + 1)
                RefreshGrid()

                ' Auto-select the latest item (nice for "dynamic" feel)
                TrySelectLastRow()
            Next
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Scan error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Dim cancelled As Boolean = (_cts IsNot Nothing AndAlso _cts.IsCancellationRequested)

            If cancelled Then
                lblStatus.Text = "Cancelled."
            Else
                lblStatus.Text = "Done."
            End If

            btnScan.Enabled = True
            btnStop.Enabled = False
            btnExport.Enabled = True
            btnCopy.Enabled = True
            clbCategories.Enabled = True
            cmbStatus.Enabled = True
            txtSearch.Enabled = True
            chkAutoInstall.Enabled = True

            _cts.Dispose()
            _cts = Nothing
        End Try
    End Sub

    Private Sub TrySelectLastRow()
        If grid.Rows.Count = 0 Then Return
        Dim idx As Integer = grid.Rows.Count - 1
        grid.ClearSelection()
        grid.Rows(idx).Selected = True
        grid.FirstDisplayedScrollingRowIndex = Math.Max(0, idx)
    End Sub

    Private Sub OnStop(sender As Object, e As EventArgs)
        If _cts Is Nothing Then Return
        _cts.Cancel()
        lblStatus.Text = "Stopping…"
        btnStop.Enabled = False
    End Sub

    Private Sub OnExport(sender As Object, e As EventArgs)
        If _results.Count = 0 Then
            MessageBox.Show("Belum ada hasil. Klik Scan dulu.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using dlg As New SaveFileDialog()
            dlg.Title = "Export DevCheck Report"
            dlg.FileName = Exporter.MakeSafeFileName("devcheck_" & DateTime.Now.ToString("yyyyMMdd_HHmm"))
            dlg.Filter = "Text report (*.txt)|*.txt|JSON (*.json)|*.json|CSV (*.csv)|*.csv"
            dlg.FilterIndex = 1

            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Try
                Dim ext As String = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant()

                If ext = ".txt" Then
                    Exporter.SaveAsTxt(dlg.FileName, _results, chkIncludeDetails.Checked)
                ElseIf ext = ".json" Then
                    Exporter.SaveAsJson(dlg.FileName, _results)
                ElseIf ext = ".csv" Then
                    Exporter.SaveAsCsv(dlg.FileName, _results)
                Else
                    ' fallback
                    Exporter.SaveAsTxt(dlg.FileName, _results, chkIncludeDetails.Checked)
                End If

                MessageBox.Show("Exported ✅", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(ex.ToString(), "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub OnCopy(sender As Object, e As EventArgs)
        If _results.Count = 0 Then
            MessageBox.Show("Belum ada hasil. Klik Scan dulu.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            Dim text As String = Exporter.BuildTextReport(_results, includeDetails:=False)
            Clipboard.SetText(text)
            lblStatus.Text = "Copied to clipboard."
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Copy error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Function GetSelectedResult() As ToolResult
        If grid.SelectedRows.Count = 0 Then Return Nothing
        Dim row As DataGridViewRow = grid.SelectedRows(0)
        Dim view As ToolRow = TryCast(row.DataBoundItem, ToolRow)
        If view Is Nothing Then Return Nothing
        Return view.Result
    End Function

    Private Async Sub OnInstallSelected(sender As Object, e As EventArgs)
        If _cts IsNot Nothing Then
            MessageBox.Show("Stop scan dulu sebelum install.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim r As ToolResult = GetSelectedResult()
        If r Is Nothing Then Return
        If r.Status = ToolStatus.Available Then
            MessageBox.Show("Tool ini sudah PUNYA.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim hint As String = InstallerCatalog.BuildHumanHint(r.Spec)
        Dim ask As DialogResult = MessageBox.Show(
            "Install tool ini sekarang?" & vbCrLf & vbCrLf &
            hint & vbCrLf & vbCrLf &
            "Catatan: beberapa installer butuh restart terminal/VS Code agar PATH kebaca.",
            "Confirm install",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question)

        If ask <> DialogResult.OK Then Return

        SetBusy(True, $"Installing {r.Spec.Name}…")

        Try
            Dim token As CancellationToken = CancellationToken.None
            Dim ir As InstallResult = Await InstallerRunner.InstallAsync(r.Spec, token)

            ' Re-check after install
            Dim re As ToolResult = Await CommandRunner.RunAsync(r.Spec, token)
            Dim installHeader As String = $"[INSTALL] Provider={ir.ProviderUsed} Success={ir.Success} ExitCode={If(ir.ExitCode.HasValue, ir.ExitCode.Value.ToString(), "-")}"
            re.FullOutput = installHeader & vbCrLf & If(ir.Output, "") & vbCrLf & vbCrLf & "----" & vbCrLf & If(re.FullOutput, "")

            ' Ensure the folder of the resolved exe is added to PATH (User + Process)
            If Not String.IsNullOrWhiteSpace(re.ResolvedPath) Then
                Try
                    Dim dir As String = System.IO.Path.GetDirectoryName(re.ResolvedPath)
                    If Not String.IsNullOrWhiteSpace(dir) Then
                        PathHelper.EnsureUserAndProcessPathContains(dir)
                    End If
                Catch
                End Try
            End If

            ' Replace existing result
            Dim idx As Integer = _results.FindIndex(Function(x) x.Spec Is r.Spec)
            If idx >= 0 Then
                _results(idx) = re
            Else
                _results.Add(re)
            End If

            RefreshGrid()
            ShowDetails(re)

            If re.Status = ToolStatus.Available Then
                MessageBox.Show("Installed ✅  (Jika masih tidak kebaca di terminal lain, restart terminal/VS Code)", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                MessageBox.Show("Install selesai, tapi tool masih belum terdeteksi. Cek panel Details untuk log.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Install error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetBusy(False, "Idle.")
        End Try
    End Sub

    Private Async Sub OnInstallMissing(sender As Object, e As EventArgs)
        If _cts IsNot Nothing Then
            MessageBox.Show("Stop scan dulu sebelum fix.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If _results.Count = 0 Then
            MessageBox.Show("Belum ada hasil scan. Klik Scan dulu.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim broken As List(Of ToolResult) = _results.Where(Function(x) x.Status <> ToolStatus.Available).ToList()
        If broken.Count = 0 Then
            MessageBox.Show("Semua tool sudah statusnya PUNYA.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim ask As DialogResult = MessageBox.Show(
            $"Fix semua tool yang statusnya TIDAK PUNYA / ERROR? (count={broken.Count})" & vbCrLf &
            "Fix = coba PATH, retry, lalu install jika perlu." & vbCrLf &
            "Catatan: proses bisa lama dan beberapa installer butuh restart terminal.",
            "Confirm fix all",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question)

        If ask <> DialogResult.OK Then Return

        SetBusy(True, "Fixing missing/error tools…")
        progress.Value = 0
        progress.Maximum = broken.Count

        Try
            Dim token As CancellationToken = CancellationToken.None

            For i As Integer = 0 To broken.Count - 1
                Dim r As ToolResult = broken(i)
                lblStatus.Text = $"Fixing {r.Spec.Name} ({i + 1}/{broken.Count})…"

                Dim fixed As ToolResult = Await AutoFixer.FixAsync(r.Spec, r, token)

                Dim idx As Integer = _results.FindIndex(Function(x) x.Spec Is r.Spec)
                If idx >= 0 Then
                    _results(idx) = fixed
                Else
                    _results.Add(fixed)
                End If

                progress.Value = Math.Min(progress.Maximum, i + 1)
                RefreshGrid()
            Next

            MessageBox.Show("Fix all selesai. Cek status di grid.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Fix error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetBusy(False, "Idle.")
        End Try
    End Sub


    Private Sub OnEnsurePathSelected(sender As Object, e As EventArgs)
        Dim r As ToolResult = GetSelectedResult()
        If r Is Nothing Then Return
        If String.IsNullOrWhiteSpace(r.ResolvedPath) Then
            MessageBox.Show("Resolved path belum ada. Install dulu atau pastikan tool ada.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim dir As String = ""
        Try
            dir = System.IO.Path.GetDirectoryName(r.ResolvedPath)
        Catch
        End Try

        If String.IsNullOrWhiteSpace(dir) Then
            MessageBox.Show("Tidak bisa menentukan folder dari path.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim changed As Boolean = False
        Try
            changed = PathHelper.EnsureUserAndProcessPathContains(dir)
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "PATH error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        If changed Then
            MessageBox.Show("PATH sudah ditambah (User + Process). Jika terminal lain belum kebaca, restart terminal/VS Code.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            MessageBox.Show("PATH sudah berisi folder tersebut.", "DevCheck", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub SetBusy(isBusy As Boolean, statusText As String)
        btnScan.Enabled = Not isBusy
        btnStop.Enabled = False
        btnExport.Enabled = Not isBusy
        btnCopy.Enabled = Not isBusy
        btnInstallMissing.Enabled = Not isBusy
        btnInstallSelected.Enabled = Not isBusy
        clbCategories.Enabled = Not isBusy
        cmbStatus.Enabled = Not isBusy
        txtSearch.Enabled = Not isBusy
        chkAutoInstall.Enabled = Not isBusy

        lblStatus.Text = statusText
    End Sub


    Private Sub OnThemeToggle(sender As Object, e As EventArgs)
        _theme = If(btnTheme.Checked, AppTheme.Dark, AppTheme.Light)
        btnTheme.Text = If(btnTheme.Checked, "Light", "Dark")
        ApplyTheme(_theme)
    End Sub

    Private Sub ApplyTheme(theme As AppTheme)
        ThemeManager.ApplyTheme(Me, theme)

        ' Make the big title label look nicer after theme changes:
        ' (walk controls safely)
        ' Note: Not required, but small polish.
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)

        If chkAutoScan.Checked Then
            BeginInvoke(New Action(Sub() btnScan.PerformClick()))
        End If
    End Sub

    Private Shared Sub EnableDoubleBuffering(dgv As DataGridView)
        Try
            Dim t As Type = GetType(DataGridView)
            t.InvokeMember("DoubleBuffered",
                           Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance Or Reflection.BindingFlags.SetProperty,
                           Nothing,
                           dgv,
                           New Object() {True})
        Catch
        End Try
    End Sub

End Class
