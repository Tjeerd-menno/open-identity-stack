# Run in an elevated PowerShell 7 session on an ephemeral GitHub-hosted Windows runner.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+-rc\.[0-9]+$')][string]$ReleaseTag,
    [Parameter(Mandatory)][ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$MigratorDigest
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$ProgressPreference = 'SilentlyContinue'
if (!$IsWindows -or $env:GITHUB_ACTIONS -ne 'true' -or $env:REHEARSAL_RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'The packaged installer modifies machine configuration. Use a disposable GitHub-hosted Windows runner.'
}

if (!$env:OIS_REHEARSAL_PHASE_FILE) {
    # Some native commands start background processes. Enforce a deadline for each
    # phase and report its name even if a child hangs with its output handles open.
    $env:OIS_REHEARSAL_PHASE_FILE = Join-Path $env:RUNNER_TEMP ("ois-phase-" + [Guid]::NewGuid())
    Set-Content $env:OIS_REHEARSAL_PHASE_FILE 'Starting rehearsal'
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh.exe).Source)
    $start.UseShellExecute = $false
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-File', $PSCommandPath, '-ReleaseTag', $ReleaseTag, '-MigratorDigest', $MigratorDigest)) {
        $start.ArgumentList.Add($argument)
    }
    $worker = [Diagnostics.Process]::Start($start)
    try {
        while (!$worker.WaitForExit(10000)) {
            $phase = Get-Item -LiteralPath $env:OIS_REHEARSAL_PHASE_FILE
            if ($phase.LastWriteTimeUtc -lt [DateTime]::UtcNow.AddMinutes(-4)) {
                $worker.Kill($true)
                throw "Rehearsal timed out during: $(Get-Content -Raw -LiteralPath $phase.FullName)"
            }
        }
        if ($worker.ExitCode -ne 0) { throw "Rehearsal failed during: $(Get-Content -Raw -LiteralPath $env:OIS_REHEARSAL_PHASE_FILE)" }
    } finally {
        $worker.Dispose()
        Remove-Item -LiteralPath $env:OIS_REHEARSAL_PHASE_FILE
        Remove-Item Env:OIS_REHEARSAL_PHASE_FILE
    }
    return
}

function Set-Phase([string]$name) {
    Set-Content -LiteralPath $env:OIS_REHEARSAL_PHASE_FILE $name
    Write-Host "Windows rehearsal phase: $name"
}

function Invoke-PostgresControl([string]$arguments) {
    # Wait for the control utility only. The PostgreSQL server must survive its parent.
    $names = Join-Path $working ([Guid]::NewGuid().ToString('N'))
    $control = Start-Process "$pgBin/pg_ctl.exe" -WindowStyle Hidden -PassThru -ArgumentList $arguments `
        -RedirectStandardInput $emptyInput -RedirectStandardOutput "$names.out" -RedirectStandardError "$names.err"
    try {
        if (!$control.WaitForExit(90000)) { throw 'pg_ctl did not exit in 90 seconds.' }
        if ($control.ExitCode -ne 0) { throw "pg_ctl exited with status $($control.ExitCode)." }
    } finally { $control.Dispose() }
}

function Get-VerifiedManifest([string]$digest) {
    $file = Join-Path $working ($digest.Substring(7) + '.json')
    Invoke-WebRequest "$registry/manifests/$digest" -Headers $registryHeaders -OutFile $file
    if ((Get-FileHash $file).Hash -ne $digest.Substring(7)) { throw 'OCI manifest checksum mismatch.' }
    Get-Content -Raw $file | ConvertFrom-Json
}

function Export-Pfx($certificate, [string]$name) {
    $path = Join-Path $working "$name.pfx"
    Export-PfxCertificate -Cert $certificate -FilePath $path -Password $pfxPassword -CryptoAlgorithmOption AES256_SHA256 | Out-Null
    $path
}

function Wait-ForHealth([string]$path) {
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        if ((Get-Service $serviceName).Status -ne 'Running') { throw 'Service exited before readiness.' }
        try {
            $response = Invoke-WebRequest "$issuer/$path" -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        } catch [System.Net.Http.HttpRequestException] {
            # Kestrel may still be binding its port or returning 503 until dependencies start.
        }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for /$path."
}

$serviceName = 'OpenIdentityStack'
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) { throw 'Service name is already in use.' }
$originalUrls = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS', 'Machine')
$working = Join-Path $env:RUNNER_TEMP ('ois-release-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $working | Out-Null
# The installer runs the API under LocalSystem. Restrict release configuration and private
# material to the runner identity and the service before writing any credentials to disk.
$runnerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
icacls.exe $working /inheritance:r /grant:r "*${runnerSid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' | Out-Null
$issuer = 'https://localhost:35431'
$recoveryIssuer = 'https://localhost:35432'
$redirect = "$issuer/auth/callback"
$package = Join-Path $working 'package'
$databaseDirectory = Join-Path $working 'pgdata'
$dbStarted = $false
$serviceInstalled = $false
$localRoot = $null
try {
    Set-Phase 'Download and verify the GitHub ZIP'
    $zipName = "open-identity-stack-api-win-x64-$ReleaseTag.zip"
    $release = gh release view $ReleaseTag -R Tjeerd-menno/open-identity-stack --json assets,isPrerelease | ConvertFrom-Json
    if (!$release.isPrerelease) { throw 'Expected a GitHub prerelease.' }
    $asset = $release.assets | Where-Object name -eq $zipName
    gh release download $ReleaseTag -R Tjeerd-menno/open-identity-stack -D $working -p $zipName -p "$zipName.sha256"
    $hash = (Get-FileHash (Join-Path $working $zipName)).Hash
    $checksum = ((Get-Content -Raw (Join-Path $working "$zipName.sha256")).Trim() -split '\s+')
    if ($checksum.Count -ne 2 -or $checksum[1] -ne $zipName -or $checksum[0] -ne $hash -or $asset.digest -ne "sha256:$hash") {
        throw 'Windows ZIP failed GitHub asset digest or published checksum verification.'
    }
    Expand-Archive -LiteralPath (Join-Path $working $zipName) -DestinationPath $package
    Write-Host "Verified Windows ZIP $ReleaseTag SHA-256 $hash"

    # The Windows ZIP contains the API. Its companion published Linux OCI image carries
    # the framework-dependent, RID-independent migrator in the final COPY /app layer.
    Set-Phase 'Fetch and verify the published migration image'
    $repository = 'tjeerd-menno/open-identity-stack-db-migrator'
    $registry = "https://ghcr.io/v2/$repository"
    $bearer = Invoke-RestMethod "https://ghcr.io/token?service=ghcr.io&scope=repository:${repository}:pull"
    $registryHeaders = @{
        Authorization = "Bearer $($bearer.token)"
        Accept = 'application/vnd.oci.image.index.v1+json,application/vnd.oci.image.manifest.v1+json,application/vnd.docker.distribution.manifest.list.v2+json,application/vnd.docker.distribution.manifest.v2+json'
    }
    $index = Get-VerifiedManifest $MigratorDigest
    $platform = @($index.manifests | Where-Object { $_.platform.os -eq 'linux' -and $_.platform.architecture -eq 'amd64' })
    if ($platform.Count -ne 1) { throw 'Expected one linux/amd64 manifest in the release index.' }
    $manifest = Get-VerifiedManifest $platform[0].digest
    $layer = $manifest.layers[-1].digest
    $archive = Join-Path $working 'migrator-layer.tar.gz'
    Invoke-WebRequest "$registry/blobs/$layer" -Headers @{Authorization=$registryHeaders.Authorization} -OutFile $archive
    if ((Get-FileHash $archive).Hash -ne $layer.Substring(7)) { throw 'Migrator layer checksum mismatch.' }
    tar.exe -xf $archive -C $working app/
    $migrator = Join-Path $working 'app/OpenIdentityStack.DbMigrator.dll'
    if (!(Test-Path -LiteralPath $migrator)) { throw 'Framework-dependent migrator is absent from the verified release layer.' }
    Write-Host "Verified migrator $MigratorDigest with application layer $layer"

    Set-Phase 'Initialize the isolated PostgreSQL cluster'
    $postgres = Get-ChildItem "${env:ProgramFiles}/PostgreSQL/*/bin/initdb.exe" | Sort-Object FullName -Descending | Select-Object -First 1
    if (!$postgres) { throw 'PostgreSQL tools missing from Windows runner.' }
    $pgBin = $postgres.DirectoryName
    $env:PGPASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $passwordFile = Join-Path $working 'db-password'
    Set-Content $passwordFile $env:PGPASSWORD -Encoding ascii
    & $postgres.FullName -D $databaseDirectory -U rehearsal --auth=scram-sha-256 "--pwfile=$passwordFile" | Out-Null
    Set-Phase 'Start the isolated PostgreSQL server'
    $emptyInput = Join-Path $working 'empty.input'
    [IO.File]::WriteAllText($emptyInput, '')
    Invoke-PostgresControl ('-D "{0}" -l "{1}" -o "-h 127.0.0.1 -p 35433" -w start' -f $databaseDirectory, (Join-Path $working 'postgres.log'))
    $dbStarted = $true
    $env:ConnectionStrings__openidentitystack = "Host=127.0.0.1;Port=35433;Database=postgres;Username=rehearsal;Password=$env:PGPASSWORD"
    $env:Secrets__EncryptionKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $env:DOTNET_ENVIRONMENT = 'Production'
    $env:Seed__AdminUser__Enabled = 'true'
    $env:Seed__AdminUser__Email = 'windows-operator@example.test'
    $env:Seed__AdminUser__Password = 'Aa7!' + [Guid]::NewGuid().ToString('N')
    $env:Seed__AdministrativeAccess__BootstrapManagementWeb = 'true'
    $env:OpenIddict__Clients__ManagementWeb__RedirectUris__0 = $redirect
    $env:OpenIddict__Clients__ManagementWeb__PostLogoutRedirectUris__0 = "$issuer/"
    $env:OpenIddict__Issuer = "$issuer/"
    Set-Phase 'Provision issuer, TLS and emergency certificates'
    $store = 'Cert:\CurrentUser\My'
    $certOptions = @{CertStoreLocation=$store; KeyExportPolicy='Exportable'; KeyAlgorithm='RSA'; KeyLength=2048; NotAfter=(Get-Date).AddDays(2)}
    $root = New-SelfSignedCertificate @certOptions -Type Custom -Subject 'CN=Release rehearsal CA' -KeyUsage CertSign,CRLSign -TextExtension '2.5.29.19={critical}{text}ca=1'
    $rootFile = Join-Path $working 'ca.cer'
    Export-Certificate -Cert $root -FilePath $rootFile | Out-Null
    $localRoot = Import-Certificate -FilePath $rootFile -CertStoreLocation 'Cert:\LocalMachine\Root'
    $server = New-SelfSignedCertificate @certOptions -DnsName localhost -Signer $root -Type SSLServerAuthentication
    $client = New-SelfSignedCertificate @certOptions -Subject 'CN=Emergency operator' -Signer $root -Type Custom -KeyUsage DigitalSignature -TextExtension '2.5.29.37={text}1.3.6.1.5.5.7.3.2'
    $signing = New-SelfSignedCertificate @certOptions -Type Custom -Subject 'CN=Issuer signing' -KeyUsage DigitalSignature
    $encryption = New-SelfSignedCertificate @certOptions -Type Custom -Subject 'CN=Issuer encryption' -KeyUsage KeyEncipherment
    $env:REHEARSAL_PFX_PASSWORD = [Guid]::NewGuid().ToString('N')
    $pfxPassword = ConvertTo-SecureString -String $env:REHEARSAL_PFX_PASSWORD -AsPlainText -Force
    $tlsPath = Export-Pfx $server 'server'
    $signPath = Export-Pfx $signing 'signing'
    $encPath = Export-Pfx $encryption 'encryption'
    $clientPath = Export-Pfx $client 'emergency'
    $env:CA_CERT = Join-Path $working 'ca.pem'
    $env:CLIENT_CERT = Join-Path $working 'emergency.pem'
    $env:CLIENT_KEY = Join-Path $working 'emergency.key'
    $openssl = (Get-Command openssl.exe -ErrorAction SilentlyContinue).Source
    if (!$openssl) { $openssl = Join-Path ${env:ProgramFiles} 'Git/usr/bin/openssl.exe' }
    & $openssl x509 -inform DER -in $rootFile -out $env:CA_CERT
    & $openssl pkcs12 -in $clientPath -passin env:REHEARSAL_PFX_PASSWORD -nokeys -clcerts -out $env:CLIENT_CERT
    & $openssl pkcs12 -in $clientPath -passin env:REHEARSAL_PFX_PASSWORD -nocerts -nodes -out $env:CLIENT_KEY
    $env:OpenIddict__Certificates__Signing__Path = $signPath
    $env:OpenIddict__Certificates__Signing__Password = $env:REHEARSAL_PFX_PASSWORD
    $env:OpenIddict__Certificates__Encryption__Path = $encPath
    $env:OpenIddict__Certificates__Encryption__Password = $env:REHEARSAL_PFX_PASSWORD

    # Keep potentially sensitive migration/HTTP diagnostics off the workflow console.
    Set-Phase 'Migrate and bootstrap the fresh database'
    & dotnet $migrator *> (Join-Path $working 'migrator.log')
    $bootstrapQuery = 'SELECT "EntityId" FROM "AuditLogEntries" WHERE "Action" = ''User.BootstrapCreated'';'
    $emergencyId = & "$pgBin/psql.exe" -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 35433 -U rehearsal -d postgres -Atc $bootstrapQuery
    if (!([Guid]::Parse($emergencyId))) { throw 'Bootstrap audit missing the emergency user ID.' }
    Write-Host 'Initialized a fresh PostgreSQL cluster and verified its administrator bootstrap audit.'

    $publicTls = @{Path=$tlsPath; Password=$env:REHEARSAL_PFX_PASSWORD}
    $config = @{
        ConnectionStrings = @{openidentitystack=$env:ConnectionStrings__openidentitystack}
        Secrets = @{EncryptionKey=$env:Secrets__EncryptionKey}
        OpenIddict = @{Issuer="$issuer/"; Certificates=@{
            Signing=@{Path=$signPath; Password=$env:REHEARSAL_PFX_PASSWORD}
            Encryption=@{Path=$encPath; Password=$env:REHEARSAL_PFX_PASSWORD}
        }}
        Kestrel = @{Endpoints=@{
            Public=@{Url=$issuer; Certificate=$publicTls}
            Recovery=@{Url=$recoveryIssuer; Certificate=$publicTls; ClientCertificateMode='AllowCertificate'}
        }}
        AdministrativeMfa = @{Emergency=@{UserId=$emergencyId; CertificateSha256=$client.GetCertHashString('SHA256')}}
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $package 'publish/appsettings.Production.json')
    Set-Phase 'Install and start the candidate service'
    & (Join-Path $package 'install-open-identity-stack.ps1') -Url $issuer
    $serviceInstalled = $true
    $dotnetRoot = Split-Path (Get-Command dotnet.exe).Source
    New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name Environment -PropertyType MultiString -Value @(
        "DOTNET_ROOT_X64=$dotnetRoot", 'ASPNETCORE_ENVIRONMENT=Production'
    ) | Out-Null
    Start-Service -Name $serviceName
    Set-Phase 'Verify readiness, liveness and discovery'
    Wait-ForHealth 'alive'
    Wait-ForHealth 'health'
    $discovery = Invoke-RestMethod "$issuer/.well-known/openid-configuration"
    if ($discovery.issuer -cne "$issuer/") { throw 'Discovery issuer mismatch.' }
    Write-Host 'Windows Service is running. Readiness, liveness and the HTTPS issuer passed.'
    $env:ISSUER = $issuer
    $env:EMERGENCY_ISSUER = $recoveryIssuer
    $env:ADMIN_EMAIL = $env:Seed__AdminUser__Email
    $env:ADMIN_PASSWORD = $env:Seed__AdminUser__Password
    $env:REDIRECT_URI = $redirect
    Set-Phase 'Authenticate the emergency administrator and exercise the Admin API'
    python (Join-Path $PSScriptRoot 'kube-admin.py')
    Set-Phase 'Uninstall the service and stop the private PostgreSQL cluster'
} finally {
    if ($serviceInstalled) { & (Join-Path $package 'uninstall-open-identity-stack.ps1') }
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $originalUrls, 'Machine')
    if ($dbStarted) { Invoke-PostgresControl ('-D "{0}" -m fast -w stop' -f $databaseDirectory) }
    if ($localRoot) { Remove-Item -LiteralPath "Cert:\LocalMachine\Root\$($localRoot.Thumbprint)" }
    # The hosted VM is discarded after the job; no private files or logs are exported.
}
