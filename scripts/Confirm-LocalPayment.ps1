[CmdletBinding()]
param(
    [int]$OrderId = 0,
    [string]$ApiBaseUrl = 'http://localhost:5278',
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$uri = [Uri]$ApiBaseUrl
if (-not $uri.IsAbsoluteUri -or -not $uri.IsLoopback -or $uri.Scheme -notin @('http', 'https') -or $uri.AbsolutePath -ne '/') {
    throw 'Este script aceita somente uma API local, por exemplo http://localhost:5278.'
}
if (-not $ValidateOnly -and $OrderId -le 0) { throw 'Informe o numero do pedido: -OrderId 123.' }
$envPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
$line = [IO.File]::ReadAllLines($envPath) |
    Where-Object { $_ -match '^\s*Payments__WebhookSecret\s*=' } |
    Select-Object -Last 1
if (-not $line) { throw 'Payments__WebhookSecret nao esta configurado no .env.' }
$secret = ($line -split '=', 2)[1].Trim().Trim('"').Trim("'")
if ([string]::IsNullOrWhiteSpace($secret)) { throw 'WebhookSecret esta vazio.' }
$targetOrder = if ($ValidateOnly) { [int]::MaxValue } else { $OrderId }
$body = [ordered]@{
    EventId = if ($ValidateOnly) { 'local-auth-check' } else { "local-test-paid-$OrderId" }
    OrderId = $targetOrder
    PaymentId = "local-test-payment-$targetOrder"
    Status = 'paid'
} | ConvertTo-Json -Compress
$bytes = [Text.Encoding]::UTF8.GetBytes($body)
$hmac = New-Object Security.Cryptography.HMACSHA256
try {
    $hmac.Key = [Text.Encoding]::UTF8.GetBytes($secret)
    $signature = ([BitConverter]::ToString($hmac.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
}
finally { $hmac.Dispose() }
$endpoint = $ApiBaseUrl.TrimEnd('/') + '/api/payments/webhook'
if ($ValidateOnly) {
    foreach ($check in @(
        @{ Signature = 'sha256=invalid'; Expected = 401 },
        @{ Signature = "sha256=$signature"; Expected = 404 }
    )) {
        try {
            Invoke-RestMethod -Method Post -Uri $endpoint -ContentType 'application/json; charset=utf-8' -Headers @{ 'X-Webhook-Signature' = $check.Signature } -Body $bytes | Out-Null
            throw 'Validacao falhou: o endpoint aceitou uma requisicao que deveria ser rejeitada.'
        }
        catch {
            if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $check.Expected) { throw }
        }
    }
    Write-Output 'Autenticacao validada: assinatura invalida = 401; assinatura correta com pedido inexistente = 404.'
    return
}
Write-Warning "Simulacao LOCAL: o pedido $OrderId sera marcado como pago, sem movimentar dinheiro."
Invoke-RestMethod -Method Post -Uri $endpoint -ContentType 'application/json; charset=utf-8' -Headers @{ 'X-Webhook-Signature' = "sha256=$signature" } -Body $bytes