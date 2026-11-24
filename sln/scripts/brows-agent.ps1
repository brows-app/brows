<#
.SYNOPSIS
  Starts, drives, and stops a Brows agent session: a Debug instance of the WPF application that an agent types
  into and observes.

.DESCRIPTION
  A session is a Debug build of Brows started with agent.pipe=<name>. The application then accepts requests on a
  named pipe that only the current user can open, and delivers keyboard input through WPF to the session's
  window, which need not be the foreground window. Each command prints a status line, any errors that Brows
  logged during the command, and a text dump of the visible UI.

  Sessions live in %LOCALAPPDATA%\Brows\agent\<session>. By default, each session starts with an empty data
  directory there instead of the user's %LOCALAPPDATA%\Brows\data.

  Commands:
    start [-Exe <path>] [-SharedData]   Start the session and wait until the palette has focus.
    input <line>                        Open the palette, type the line, and press ENTER.
    type <text>                         Type text into the element that has keyboard focus.
    press <keys>                        Press gestures written as in trigger.conf, such as "CTRL+L" or "DOWN ENTER".
    ui                                  Print the UI text dump.
    windows                             List the session's windows.
    wait [<text>] [-Absent]             Wait until the UI settles, or until it contains (or lacks) the text.
    screenshot [<path>]                 Save a PNG of the window, by default in the session's shots directory.
    log [-Since <n>]                    Print the session's log entries.
    stop [-Force]                       End the session, through the palette's exit command unless -Force.

.EXAMPLE
  brows-agent.ps1 start

.EXAMPLE
  brows-agent.ps1 input panel-add

.EXAMPLE
  brows-agent.ps1 press CTRL+W -Session second
#>
#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('start', 'input', 'type', 'press', 'ui', 'windows', 'wait', 'screenshot', 'log', 'stop')]
    [string] $Command,

    [Parameter(Position = 1)]
    [string] $Value,

    [ValidatePattern('^[A-Za-z0-9_-]{1,32}$')]
    [string] $Session = 'default',

    [long] $Window = 0,

    [string] $Exe,

    [switch] $SharedData,

    [switch] $Absent,

    [long] $Since = 0,

    [switch] $NoSettle,

    [ValidateRange(1, 10000)]
    [int] $MaxLines = 400,

    [ValidateRange(1, 3600)]
    [int] $TimeoutSeconds = 10,

    [ValidateRange(1, 3600)]
    [int] $StartTimeoutSeconds = 60,

    [switch] $Force,

    [switch] $Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The session directory ignores case but the session mutex does not, so use one spelling for both.
$Session = $Session.ToLowerInvariant()
$LocalData =[Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$SessionDirectory = Join-Path -Path $LocalData -ChildPath "Brows\agent\$Session"
$ManifestPath = Join-Path -Path $SessionDirectory -ChildPath 'session.json'
$PendingManifestPath = Join-Path -Path $SessionDirectory -ChildPath 'session.pending.json'
$BrowsLogPath = Join-Path -Path $LocalData -ChildPath 'Brows\log\brows.log'
$CollectedErrors = [System.Collections.Generic.List[string]]::new()

function New-Failure {
    param(
        [string] $Message,
        [object] $Response
    )

    $exception = [System.InvalidOperationException]::new($Message)
    $exception.Data['response'] = $Response
    return $exception
}

function Read-Manifest {
    param([switch] $Recover)

    $file = $ManifestPath
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        if (-not $Recover -or -not (Test-Path -LiteralPath $PendingManifestPath -PathType Leaf)) {
            return $null
        }
        $file = $PendingManifestPath
    }
    try {
        $manifest = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        if ($null -eq $manifest) {
            throw 'The session record has no process identity.'
        }
    }
    catch {
        $message = "The session record at '$file' is unreadable or incomplete. " +
            'Verify that the launched process has exited before removing it.'
        throw [System.InvalidOperationException]::new($message, $_.Exception)
    }
    return $manifest | Add-Member -NotePropertyName ManifestFile -NotePropertyValue $file -PassThru
}

function Get-SessionProcess {
    param(
        [object] $Manifest
    )

    if ($null -eq $Manifest) {
        return $null
    }
    $processId = [int]$Manifest.pid
    try {
        $process = [System.Diagnostics.Process]::GetProcessById($processId)
    }
    catch [System.ArgumentException] {
        return $null
    }
    $verified = $false
    try {
        # Reading Handle makes the object keep a handle, which keeps the process id from being reused, so
        # the identity checked here is the process that is later waited on and killed.
        $handle = $process.Handle
        if ($null -eq $handle -or $handle -eq [IntPtr]::Zero) {
            throw 'Could not open a handle to the process.'
        }
        if ($process.HasExited) {
            return $null
        }
        if ($process.StartTime.ToUniversalTime().Ticks -ne $Manifest.startTime) {
            return $null
        }
        $path = $process.Path
        if (-not $path) {
            if ($process.HasExited) {
                return $null
            }
            throw 'Could not read the executable path.'
        }
        $verified = [string]::Equals($path, $Manifest.path, [StringComparison]::OrdinalIgnoreCase)
        if ($verified) {
            return $process
        }
    }
    catch {
        $message = "Could not verify session '$Session' process ${processId}. The session record and data were kept. " +
            $_.Exception.Message
        throw [System.InvalidOperationException]::new($message, $_.Exception)
    }
    finally {
        if (-not $verified) {
            $process.Dispose()
        }
    }
}

function Use-SessionLock {
    param(
        [scriptblock] $Body
    )

    # The session directory is shared by every logon session of the user, so the lock must be too: a Global
    # name, qualified by the user's SID.
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex = [System.Threading.Mutex]::new($false, "Global\BrowsAgentSession.$sid.$Session")
    $owned = $false
    try {
        try {
            $owned = $mutex.WaitOne([TimeSpan]::FromSeconds(30))
        }
        catch [System.Threading.AbandonedMutexException] {
            $owned = $true
        }
        if (-not $owned) {
            throw "Another command is using session '$Session'."
        }
        & $Body
    }
    finally {
        if ($owned) {
            $mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
}

function Send-Request {
    param(
        [string] $Pipe,
        [hashtable] $Request,
        [int] $ConnectMilliseconds = 2000,

        # When positive, bounds the connection and the read, together, to this many milliseconds.
        [int] $WithinMilliseconds = 0
    )

    if (-not $Request.ContainsKey('timeout')) {
        $Request['timeout'] = $TimeoutSeconds * 1000
    }
    $readTimeout = [int]$Request['timeout'] + 5000
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    if ($WithinMilliseconds -gt 0) {
        $ConnectMilliseconds = [Math]::Min($ConnectMilliseconds, $WithinMilliseconds)
    }
    $json = $Request | ConvertTo-Json -Compress -Depth 4
    $client = [System.IO.Pipes.NamedPipeClientStream]::new(
        '.',
        $Pipe,
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::CurrentUserOnly)
    try {
        $client.Connect($ConnectMilliseconds)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("$json`n")
        # No Flush: on a pipe it waits until the server has read the request, which no deadline bounds.
        $client.Write($bytes, 0, $bytes.Length)
        $reader = [System.IO.StreamReader]::new($client, [System.Text.UTF8Encoding]::new($false))
        $read = $reader.ReadLineAsync()
        if ($WithinMilliseconds -gt 0) {
            $left = $WithinMilliseconds - $clock.ElapsedMilliseconds
            $readTimeout = [int][Math]::Max(0, [Math]::Min($readTimeout, $left))
        }
        if (-not $read.Wait($readTimeout)) {
            throw "Brows did not answer within $readTimeout ms."
        }
        $line = $read.Result
        if ($null -eq $line) {
            throw 'Brows closed the connection without answering.'
        }
        $response = $line | ConvertFrom-Json
        foreach ($item in $response.errors) {
            if (-not $CollectedErrors.Contains($item)) {
                $CollectedErrors.Add($item)
            }
        }
        return $response
    }
    finally {
        $client.Dispose()
    }
}

function Get-SessionManifest {
    $manifest = Read-Manifest
    $process = Get-SessionProcess -Manifest $manifest
    if ($null -eq $process) {
        $stale = if ($null -eq $manifest) { '' } else { ' Its manifest is stale.' }
        throw "Session '$Session' is not running.$stale Start it with: brows-agent.ps1 start -Session $Session"
    }
    $process.Dispose()
    return $manifest
}

function Send-SessionRequest {
    param(
        [hashtable] $Request,
        [long] $Target = $Window
    )

    if ($Target -ne 0) {
        $Request['window'] = $Target
    }
    $manifest = Get-SessionManifest
    $response = Send-Request -Pipe $manifest.pipe -Request $Request
    if ($response.pid -ne $manifest.pid) {
        throw "The session's pipe is served by process $($response.pid), not by the session's process $($manifest.pid)."
    }
    if (-not $response.ok) {
        throw (New-Failure -Message $response.error -Response $response)
    }
    return $response
}

function Format-Focus {
    param(
        [object] $Response
    )

    $focus = $Response.focus
    if ($null -eq $focus) {
        return 'focus=none'
    }
    $text = "focus=$($focus.type)"
    if ($focus.name) {
        $text += " $($focus.name)"
    }
    if ($focus.owner) {
        $text += " in $($focus.owner)"
    }
    return $text
}

function Write-Response {
    param(
        [object] $Response,
        [string[]] $Notes = @()
    )

    if ($Json) {
        if ($null -ne $Response) {
            $Response.errors = @($CollectedErrors)
            $Response | ConvertTo-Json -Depth 8
        }
        return
    }
    if ($null -eq $Response) {
        return
    }
    $parts = @(if ($Response.ok) { 'ok' } else { 'FAILED' })
    if ($null -ne $Response.settled) {
        $parts += "settled=$($Response.settled.ToString().ToLowerInvariant())"
    }
    if ($null -ne $Response.window) {
        $parts += "window=$($Response.window.id) `"$($Response.window.title)`""
        $parts += Format-Focus -Response $Response
    }
    if ($null -ne $Response.requested) {
        $parts += "delivered=$($Response.delivered)/$($Response.requested)"
    }
    $parts -join ' '
    if ($Response.error) {
        "error: $($Response.error)"
    }
    foreach ($item in $CollectedErrors) {
        "logged error: $item"
    }
    foreach ($note in $Notes) {
        "note: $note"
    }
    if ($null -ne $Response.windows) {
        foreach ($item in $Response.windows) {
            $active = if ($item.active) { ' active' } else { '' }
            "window $($item.id) index $($item.index) `"$($item.title)`"$active $($item.width)x$($item.height)"
        }
    }
    if ($null -ne $Response.log) {
        foreach ($item in $Response.log) {
            $time = if ($item.time -is [datetime]) { $item.time.ToString('HH:mm:ss.fff') } else { $item.time }
            "$($item.seq) $time $($item.severity) $($item.name): $($item.text)"
        }
    }
    if ($Response.path) {
        "path: $($Response.path)"
    }
    if ($Response.empty) {
        'note: the screenshot is empty, which usually means the session is locked.'
    }
    if ($Response.ui) {
        $Response.ui
    }
}

function Write-Failure {
    param(
        [System.Management.Automation.ErrorRecord] $ErrorRecord
    )

    # The failure is the client's, even when the response it carries for diagnosis reports success, so report
    # that response's observations under the client's failure.
    $exception = $ErrorRecord.Exception
    $observed = $exception.Data['response']
    if ($null -eq $observed) {
        $failure = [pscustomobject]@{ ok = $false; error = $exception.Message; errors = @() }
    }
    else {
        $failure = $observed.PSObject.Copy()
        $failure.ok = $false
        $failure.error = $exception.Message
    }
    if ($Json) {
        Write-Response -Response $failure
        return
    }
    if ($null -eq $observed) {
        'FAILED'
        "error: $($exception.Message)"
        foreach ($item in $CollectedErrors) {
            "logged error: $item"
        }
        return
    }
    Write-Response -Response $failure
}

function Get-BrowsLogTail {
    if (Test-Path -LiteralPath $BrowsLogPath -PathType Leaf) {
        return Get-Content -LiteralPath $BrowsLogPath -Tail 40
    }
    return @()
}

function Resolve-FileSystemPath {
    param([string] $Path)

    $provider = $null
    $drive = $null
    $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
        $Path, [ref]$provider, [ref]$drive)
    if ($provider.Name -ne 'FileSystem') {
        throw "Specify a filesystem path instead of '$Path'."
    }
    return $resolved
}

function Start-Session {
    $exePath = if ($Exe) { $Exe } else { Join-Path -Path $PSScriptRoot -ChildPath '..\out\Debug\brows.exe' }
    $exePath = Resolve-FileSystemPath -Path $exePath
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        throw "Brows is not built at $exePath. Build the Debug configuration first (see the brows-code-building skill)."
    }
    $started = Use-SessionLock {
        $manifest = Read-Manifest -Recover
        $running = Get-SessionProcess -Manifest $manifest
        if ($null -ne $running) {
            $running.Dispose()
            throw "Session '$Session' is already running as process $($manifest.pid). " +
                "Stop it with: brows-agent.ps1 stop -Session $Session, or use another -Session."
        }
        if ($null -ne $manifest) {
            Remove-Item -LiteralPath $manifest.ManifestFile
        }
        New-Item -ItemType Directory -Path $SessionDirectory -Force | Out-Null
        $dataRoot = $null
        if (-not $SharedData) {
            $dataRoot = Join-Path -Path $SessionDirectory -ChildPath 'data'
            if (Test-Path -LiteralPath $dataRoot) {
                Remove-Item -LiteralPath $dataRoot -Recurse -Force
            }
            New-Item -ItemType Directory -Path $dataRoot | Out-Null
        }
        $pipe = "brows-agent-$Session-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
        $previousDataRoot = $env:BROWS_DATA_ROOT
        $process = $null
        # Reserve a recovery record before launching. It stays private to the session lock until publication.
        [IO.File]::Open($PendingManifestPath, [IO.FileMode]::CreateNew).Dispose()
        try {
            try {
                if ($null -eq $dataRoot) {
                    Remove-Item -Path Env:BROWS_DATA_ROOT -ErrorAction SilentlyContinue
                }
                else {
                    $env:BROWS_DATA_ROOT = $dataRoot
                }
                $process = Start-Process `
                    -FilePath $exePath `
                    -WorkingDirectory (Split-Path -Path $exePath -Parent) `
                    -ArgumentList 'splash.show=false', "agent.pipe=$pipe" `
                    -PassThru
            }
            finally {
                $env:BROWS_DATA_ROOT = $previousDataRoot
            }
            $path = $process.Path
            if (-not $path) {
                if (-not $process.HasExited) {
                    throw "Could not read the executable path of process $($process.Id)."
                }
                $path = $exePath
            }
            $record = [ordered]@{
                pid = $process.Id
                startTime = $process.StartTime.ToUniversalTime().Ticks
                path = $path
                pipe = $pipe
                dataRoot = $dataRoot
            } | ConvertTo-Json
            [IO.File]::WriteAllText($PendingManifestPath, $record)
            [IO.File]::Move($PendingManifestPath, $ManifestPath)
        }
        catch {
            $failure = $_.Exception
            if ($null -ne $process) {
                try {
                    if (-not $process.HasExited) {
                        $process.Kill()
                        if (-not $process.WaitForExit(5000)) {
                            throw 'The process did not exit within 5 seconds.'
                        }
                    }
                }
                catch {
                    $message = "Could not publish session '$Session' or confirm exit of process $($process.Id). " +
                        "The recovery record was kept at '$PendingManifestPath'. " +
                        "Publication failed: $($failure.Message) Cleanup failed: $($_.Exception.Message)"
                    throw [System.InvalidOperationException]::new($message, $failure)
                }
                finally {
                    $process.Dispose()
                }
            }
            [IO.File]::Delete($PendingManifestPath)
            throw
        }
        [pscustomobject]@{ Process = $process; Pipe = $pipe }
    }
    $process = $started.Process
    $pipe = $started.Pipe
    # One monotonic budget bounds the whole startup, including every connection and read.
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $remaining = { [int][Math]::Max(0, $StartTimeoutSeconds * 1000 - $clock.ElapsedMilliseconds) }
    $stillRunning = 'The session is still running; inspect it with ui or screenshot, or end it with stop.'
    $notReady = "Brows was not ready within $StartTimeoutSeconds s. $stillRunning"
    $last = $null
    for (; ; ) {
        if ($process.HasExited) {
            $tail = Get-BrowsLogTail
            throw "Brows exited with code $($process.ExitCode) before it was ready. The agent bridge runs only in " +
                "Debug builds. The end of $BrowsLogPath, which all instances share, follows:`n$($tail -join "`n")"
        }
        if ((& $remaining) -le 0) {
            $tail = Get-BrowsLogTail
            $observed = if ($null -eq $last) {
                'The pipe never answered.'
            }
            else {
                "Last seen: $(Format-Focus -Response $last)."
            }
            throw "The palette did not get keyboard focus within $StartTimeoutSeconds s. $observed $stillRunning " +
                "The end of $BrowsLogPath follows:`n$($tail -join "`n")"
        }
        try {
            # At least 1, because 0 would leave the request unbounded.
            $budget = [Math]::Max(1, (& $remaining))
            $request = @{ op = 'windows'; timeout = [Math]::Min(2000, $budget) }
            $last = Send-Request -Pipe $pipe -Request $request -ConnectMilliseconds 250 -WithinMilliseconds $budget
        }
        catch {
            Start-Sleep -Milliseconds 250
            continue
        }
        $paletteIsReady = $last.ok -and $null -ne $last.palette -and $last.palette.focused
        if ($paletteIsReady) {
            break
        }
        Start-Sleep -Milliseconds 250
    }
    # Settle and check within what is left of the same budget. The server gets a little less than the client
    # waits, so that it can answer before the client gives up.
    $target = $last.window.id
    $unsettled = "The UI did not settle within $StartTimeoutSeconds s. $stillRunning"
    $budget = & $remaining
    if ($budget -le 0) {
        throw (New-Failure -Message $unsettled -Response $last)
    }
    $request = @{ op = 'wait'; window = $target; maxLines = $MaxLines; timeout = [Math]::Max(1, $budget - 250) }
    $settled = Send-Request -Pipe $pipe -Request $request -WithinMilliseconds $budget
    if (-not ($settled.ok -and $settled.settled)) {
        throw (New-Failure -Message $unsettled -Response $settled)
    }
    $budget = & $remaining
    if ($budget -le 0) {
        throw (New-Failure -Message $notReady -Response $settled)
    }
    $request = @{ op = 'windows'; window = $target; timeout = [Math]::Max(1, $budget - 250) }
    $check = Send-Request -Pipe $pipe -Request $request -WithinMilliseconds $budget
    if (-not ($check.ok -and $check.palette.focused)) {
        $message = "The palette lost keyboard focus while the window settled. $stillRunning"
        throw (New-Failure -Message $message -Response $settled)
    }
    if ((& $remaining) -le 0) {
        throw (New-Failure -Message $notReady -Response $settled)
    }
    return $settled
}

function Submit-PaletteLine {
    param(
        [string] $Line
    )

    $target = $Window
    if ($target -eq 0) {
        $state = Send-SessionRequest -Request @{ op = 'windows' }
        if ($null -eq $state.window) {
            throw (New-Failure -Message 'Brows has no open window.' -Response $state)
        }
        $target = $state.window.id
    }
    $state = Send-SessionRequest -Request @{ op = 'windows' } -Target $target
    if (-not $state.palette.open) {
        $state = Send-SessionRequest -Request @{ op = 'press'; keys = 'CTRL+L' } -Target $target
        if (-not $state.palette.focused) {
            $message = 'The palette did not open. Check the cmd[Palette] trigger in trigger.conf.'
            throw (New-Failure -Message $message -Response $state)
        }
    }
    # Brows checks that the palette input still has keyboard focus before every key and character, so input
    # never reaches a panel if the palette closes between these requests.
    $request = @{ op = 'press'; keys = 'CTRL+A'; settle = $false; requirePalette = $true }
    Send-SessionRequest -Request $request -Target $target | Out-Null
    $request = @{ op = 'type'; text = $Line; maxLines = $MaxLines; requirePalette = $true }
    $state = Send-SessionRequest -Request $request -Target $target
    $inputIsLine = $state.palette.focused -and $state.palette.text -ceq $Line
    if (-not $inputIsLine) {
        $message = "The palette input is `"$($state.palette.text)`" instead of the line, so ENTER was not pressed."
        throw (New-Failure -Message $message -Response $state)
    }
    # Brows also checks the palette's text just before ENTER, in case it changed after the type request.
    $request = @{
        op = 'press'
        keys = 'ENTER'
        expect = $Line
        requirePalette = $true
        settle = -not $NoSettle
        maxLines = $MaxLines
    }
    $state = Send-SessionRequest -Request $request -Target $target
    $notes = @()
    $paletteStillHasLine = $state.palette.focused -and $state.palette.text -ceq $Line
    if ($paletteStillHasLine) {
        $notes += 'the palette is still open; the input may not have matched a command.'
    }
    return [pscustomobject]@{ Response = $state; Notes = $notes }
}

function New-StopResult {
    param(
        [string] $Outcome,
        [object] $ProcessId,
        [string] $Message,
        [bool] $Ok = $true
    )

    return [pscustomobject]@{
        ok = $Ok
        outcome = $Outcome
        pid = $ProcessId
        message = $Message
        errors = @()
    }
}

function Stop-Session {
    Use-SessionLock {
        $manifest = Read-Manifest -Recover
        if ($null -eq $manifest) {
            return New-StopResult -Outcome 'absent' -ProcessId $null -Message "Session '$Session' is not running."
        }
        $process = Get-SessionProcess -Manifest $manifest
        if ($null -eq $process) {
            Remove-Item -LiteralPath $manifest.ManifestFile
            $message = "Session '$Session' was not running. " +
                "Its stale manifest, which named process $($manifest.pid), was removed."
            return New-StopResult -Outcome 'stale' -ProcessId $manifest.pid -Message $message
        }
        try {
            if (-not $Force) {
                try {
                    Submit-PaletteLine -Line 'exit' | Out-Null
                }
                catch {
                    Write-Verbose "The exit command did not finish cleanly: $($_.Exception.Message)"
                }
                $process.WaitForExit(15000) | Out-Null
            }
            if (-not $process.HasExited) {
                $process.Kill()
                if (-not $process.WaitForExit(5000)) {
                    # Keep the manifest, so that the session can still be stopped once the process exits.
                    $message = "Process $($process.Id) of session '$Session' did not exit within 5 s of being " +
                        'killed. The session manifest was kept; run stop again.'
                    return New-StopResult -Outcome 'timeout' -ProcessId $process.Id -Message $message -Ok $false
                }
            }
            Remove-Item -LiteralPath $manifest.ManifestFile
            $message = "Stopped session '$Session' (process $($process.Id))."
            return New-StopResult -Outcome 'stopped' -ProcessId $process.Id -Message $message
        }
        finally {
            $process.Dispose()
        }
    }
}

function Write-StopResult {
    param(
        [object] $Result
    )

    $Result.errors = @($CollectedErrors)
    if ($Json) {
        $Result | ConvertTo-Json -Depth 4
        return
    }
    if ($Result.ok) {
        $Result.message
    }
    else {
        'FAILED'
        "error: $($Result.message)"
    }
    foreach ($item in $CollectedErrors) {
        "logged error: $item"
    }
}

function Get-RequiredValue {
    param(
        [string] $Name
    )

    if ([string]::IsNullOrEmpty($Value)) {
        throw "Specify the $Name, as in: brows-agent.ps1 $Command <$Name>"
    }
    return $Value
}

try {
    switch ($Command) {
        'start' {
            Write-Response -Response (Start-Session)
        }
        'input' {
            $result = Submit-PaletteLine -Line (Get-RequiredValue -Name 'line')
            Write-Response -Response $result.Response -Notes $result.Notes
        }
        'type' {
            $request = @{
                op = 'type'
                text = Get-RequiredValue -Name 'text'
                settle = -not $NoSettle
                maxLines = $MaxLines
            }
            Write-Response -Response (Send-SessionRequest -Request $request)
        }
        'press' {
            $request = @{
                op = 'press'
                keys = Get-RequiredValue -Name 'keys'
                settle = -not $NoSettle
                maxLines = $MaxLines
            }
            Write-Response -Response (Send-SessionRequest -Request $request)
        }
        'ui' {
            Write-Response -Response (Send-SessionRequest -Request @{ op = 'ui'; maxLines = $MaxLines })
        }
        'windows' {
            Write-Response -Response (Send-SessionRequest -Request @{ op = 'windows' })
        }
        'wait' {
            $request = @{ op = 'wait'; absent = $Absent.IsPresent; maxLines = $MaxLines }
            if ($Value) {
                $request['text'] = $Value
            }
            Write-Response -Response (Send-SessionRequest -Request $request)
        }
        'screenshot' {
            $path = $Value
            if (-not $path) {
                $name = "$([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')).png"
                $path = Join-Path -Path $SessionDirectory -ChildPath "shots\$name"
            }
            $path = Resolve-FileSystemPath -Path $path
            Write-Response -Response (Send-SessionRequest -Request @{ op = 'screenshot'; path = $path })
        }
        'log' {
            Write-Response -Response (Send-SessionRequest -Request @{ op = 'log'; since = $Since })
        }
        'stop' {
            $result = Stop-Session
            Write-StopResult -Result $result
            if (-not $result.ok) {
                exit 1
            }
        }
    }
}
catch {
    Write-Failure -ErrorRecord $_
    exit 1
}
