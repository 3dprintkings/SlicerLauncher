
$path = "C:\SlicerLauncher\SlicerLauncher\Program.cs"

if (-not (Test-Path $path)) {
    Write-Error "Program.cs not found at $path"
    exit 1
}

$text = Get-Content $path -Raw

$pattern = '(?s)\s*var countdownLabel = new Label.*?var secLabel = new Label.*?;\r?\n'

$replacement = @'
        var countdownRow = new FlowLayoutPanel
        {
            Location = new Point(18, 100),
            Size = new Size(340, 36),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };

        var countdownLabel = new Label
        {
            Text = "Countdown before launch",
            AutoSize = true,
            Margin = new Padding(0, 7, 12, 0)
        };

        _countdownSeconds.Minimum = 1;
        _countdownSeconds.Maximum = 30;
        _countdownSeconds.Value = Math.Clamp(_settings.CountdownSeconds, 1, 30);
        _countdownSeconds.AutoSize = false;
        _countdownSeconds.Size = new Size(70, 28);
        _countdownSeconds.TextAlign = HorizontalAlignment.Center;
        _countdownSeconds.Margin = new Padding(0, 2, 10, 0);

        var secLabel = new Label
        {
            Text = "seconds",
            AutoSize = true,
            Margin = new Padding(0, 7, 0, 0)
        };

        countdownRow.Controls.Add(countdownLabel);
        countdownRow.Controls.Add(_countdownSeconds);
        countdownRow.Controls.Add(secLabel);
'@

$newText = [regex]::Replace($text, $pattern, "`r`n$replacement`r`n", 1)

if ($newText -eq $text) {
    Write-Error "Countdown block was not found. No changes were made."
    exit 1
}

$newText = $newText.Replace(
'        autoGroup.Controls.Add(countdownLabel);' + "`r`n" +
'        autoGroup.Controls.Add(_countdownSeconds);' + "`r`n" +
'        autoGroup.Controls.Add(secLabel);',
'        autoGroup.Controls.Add(countdownRow);'
)

$newText = $newText.Replace(
'        autoGroup.Controls.Add(countdownLabel);' + "`n" +
'        autoGroup.Controls.Add(_countdownSeconds);' + "`n" +
'        autoGroup.Controls.Add(secLabel);',
'        autoGroup.Controls.Add(countdownRow);'
)

# Give the group enough vertical room for the dedicated row.
$newText = $newText.Replace('Size = new Size(500, 145),', 'Size = new Size(500, 155),')

Set-Content -Path $path -Value $newText -Encoding UTF8

Write-Host "Done. Countdown row patched in:"
Write-Host $path
Write-Host ""
Write-Host "The countdown row now uses FlowLayoutPanel, so labels cannot overlap the number field even with Windows display scaling."
