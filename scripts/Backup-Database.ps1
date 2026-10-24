[CmdletBinding()]
param([string]$DestinationDirectory = (Join-Path $PSScriptRoot '../.local-backups'))
$ErrorActionPreference='Stop'
$directory=[IO.Path]::GetFullPath($DestinationDirectory)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$name='pim4-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)+'.dump'
$remote='/tmp/'+$name
$destination=Join-Path $directory $name
try {
 docker exec medishop-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --no-owner --no-acl -f "$1"' -- $remote
 if($LASTEXITCODE -ne 0){throw 'Falha ao gerar backup.'}
 docker cp ('medishop-postgres:'+$remote) $destination
 if($LASTEXITCODE -ne 0 -or (Get-Item -LiteralPath $destination).Length -eq 0){throw 'Falha ao copiar backup.'}
 Get-FileHash -LiteralPath $destination -Algorithm SHA256 | Format-List Algorithm,Hash,Path | Out-Host
 Write-Output $destination
} finally { docker exec medishop-postgres rm -f $remote | Out-Null }