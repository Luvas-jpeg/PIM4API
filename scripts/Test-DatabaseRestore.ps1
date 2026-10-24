[CmdletBinding()]
param([Parameter(Mandatory)][string]$BackupPath)
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $BackupPath).Path
$name='pim4_restore_test_'+[Guid]::NewGuid().ToString('N')
if($name -notmatch '^pim4_restore_test_[a-f0-9]{32}$'){throw 'Destino de teste invalido.'}
$remote='/tmp/'+$name+'.dump'
$created=$false
try {
 docker cp $source ('medishop-postgres:'+$remote)
 if($LASTEXITCODE -ne 0){throw 'Falha ao copiar backup para validacao.'}
 docker exec medishop-postgres sh -c 'createdb -U "$POSTGRES_USER" "$1"' -- $name
 if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco isolado de restauracao.'}
 $created=$true
 docker exec medishop-postgres sh -c 'pg_restore -U "$POSTGRES_USER" --exit-on-error --no-owner --no-acl -d "$1" "$2"' -- $name $remote
 if($LASTEXITCODE -ne 0){throw 'Falha ao restaurar o backup.'}
 $sql='SELECT json_build_object(''tables'',(SELECT count(*) FROM information_schema.tables WHERE table_schema=''public'' AND table_type=''BASE TABLE''),''courses'',(SELECT count(*) FROM "Courses"),''orders'',(SELECT count(*) FROM "Orders"),''enrollments'',(SELECT count(*) FROM "Enrollments"),''migrations'',(SELECT count(*) FROM "__EFMigrationsHistory"));'
 $expected=($sql | docker exec -i medishop-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -qAt') -join ''
 if($LASTEXITCODE -ne 0){throw 'Falha ao conferir banco de origem.'}
 $actual=($sql | docker exec -i medishop-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$1" -v ON_ERROR_STOP=1 -qAt' -- $name) -join ''
 if($LASTEXITCODE -ne 0 -or $expected -ne $actual){throw 'Contagens divergentes: confira se houve alteracoes no banco durante o backup.'}
 Write-Output ('Restauracao validada em banco isolado: '+$actual)
} finally {
 if($created){docker exec medishop-postgres sh -c 'dropdb -U "$POSTGRES_USER" "$1"' -- $name | Out-Null}
 docker exec medishop-postgres rm -f $remote | Out-Null
}