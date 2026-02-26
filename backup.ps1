# Sistema de Backups Simple para Wild
# Uso: 
# 1. Crear backup: .\backup.ps1 create "motivo"
# 2. Restaurar backup: .\backup.ps1 restore
# 3. Restaurar backup específico: .\backup.ps1 restore "nombre_backup"

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("create", "restore")]
    [string]$Action = "create",
    
    [Parameter(Mandatory=$false)]
    [string]$Reason = "",
    
    [Parameter(Mandatory=$false)]
    [string]$BackupName = ""
)

# Configuración
$BackupDir = "backups"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Archivos y carpetas a excluir del backup
$ExcludePaths = @(
    "backups",
    ".godot",
    "*.tmp",
    "*.log"
)

# Función para solicitar motivo de manera interactiva
function Get-BackupReason {
    param(
        [string]$Title = "Crear Backup",
        [string]$Prompt = "Ingrese el motivo del backup:"
    )
    
    # Usar Read-Host en lugar de InputBox (más compatible)
    Write-Host "$Prompt" -ForegroundColor Yellow
    $reason = Read-Host
    
    # Si el usuario deja vacío, usar valor por defecto
    if ([string]::IsNullOrEmpty($reason)) {
        $reason = "manual"
    }
    
    return $reason
}

function Create-Backup {
    param([string]$BackupReason)
    
    # Obtener motivo de manera interactiva si no se proporcionó
    if ([string]::IsNullOrEmpty($BackupReason)) {
        $BackupReason = Get-BackupReason
    }
    
    $BackupName = "$Timestamp`_$BackupReason"
    $BackupPath = Join-Path $BackupDir $BackupName
    
    Write-Host "Creando backup: $BackupName" -ForegroundColor Green
    Write-Host "Destino: $BackupPath" -ForegroundColor Blue
    
    try {
        # Crear directorio de backup
        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
        
        # Copiar todo el proyecto excepto las exclusiones
        Get-ChildItem -Path "." -Recurse | ForEach-Object {
            $shouldExclude = $false
            
            # Verificar si la ruta está en la lista de exclusión
            foreach ($exclude in $ExcludePaths) {
                if ($_.FullName -like "*$exclude*" -or $_.Name -like $exclude) {
                    $shouldExclude = $true
                    break
                }
            }
            
            if (-not $shouldExclude) {
                $relativePath = $_.FullName.Replace((Get-Location).Path, "").TrimStart('\')
                $destPath = Join-Path $BackupPath $relativePath
                
                if ($_.PSIsContainer) {
                    # Es un directorio
                    if (!(Test-Path $destPath)) {
                        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
                    }
                    Write-Host "Directorio: $relativePath" -ForegroundColor Cyan
                } else {
                    # Es un archivo
                    $destDir = Split-Path $destPath -Parent
                    if (!(Test-Path $destDir)) {
                        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                    }
                    Copy-Item $_.FullName $destPath -Force
                    Write-Host "Archivo: $relativePath" -ForegroundColor Green
                }
            }
        }
        
        # Crear ZIP del backup
        $zipPath = "$BackupPath.zip"
        Compress-Archive -Path $BackupPath -DestinationPath $zipPath -Force
        
        # Eliminar directorio temporal, mantener solo el ZIP
        Remove-Item $BackupPath -Recurse -Force
        
        Write-Host "Backup completado: $zipPath" -ForegroundColor Green
        
        # Escribir en latest.log
        $logMessage = "Backup: $BackupName.zip creado - $BackupReason"
        Add-Content -Path "latest.log" -Value "[$(Get-Date -Format 'HH:mm:ss.fff')] $logMessage"
        
    } catch {
        Write-Host "Error creando backup: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

function Restore-Backup {
    param([string]$BackupName)
    
    if ([string]::IsNullOrEmpty($BackupName)) {
        # Restaurar último backup ZIP
        $backups = Get-ChildItem $BackupDir -File | Where-Object { $_.Extension -eq ".zip" } | Sort-Object LastWriteTime -Descending
        if ($backups.Count -eq 0) {
            Write-Host "No hay backups disponibles" -ForegroundColor Red
            exit 1
        }
        $BackupName = $backups[0].Name
    }
    
    $BackupPath = Join-Path $BackupDir $BackupName
    
    if (!(Test-Path $BackupPath)) {
        Write-Host "No existe el backup: $BackupName" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Restaurando desde: $BackupName" -ForegroundColor Green
    
    try {
        # Extraer ZIP en directorio temporal
        $tempPath = Join-Path $BackupDir "temp_restore"
        if (Test-Path $tempPath) {
            Remove-Item $tempPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
        
        Expand-Archive -Path $BackupPath -DestinationPath $tempPath -Force
        
        # Restaurar archivos desde el directorio temporal
        $files = Get-ChildItem $tempPath -File -Recurse
        foreach ($file in $files) {
            $relativePath = $file.FullName.Replace($tempPath, "").TrimStart('\')
            $destPath = $relativePath
            
            $destDir = Split-Path $destPath -Parent
            if (!(Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            
            Copy-Item $file.FullName $destPath -Force
            Write-Host "Restaurado: $relativePath" -ForegroundColor Green
        }
        
        # Limpiar directorio temporal
        Remove-Item $tempPath -Recurse -Force
        
        Write-Host "Restauración completada desde: $BackupName" -ForegroundColor Green
        
        # Escribir en latest.log
        $logMessage = "Restore: $BackupName restaurado"
        Add-Content -Path "latest.log" -Value "[$(Get-Date -Format 'HH:mm:ss.fff')] $logMessage"
        
    } catch {
        Write-Host "Error restaurando: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Ejecutar acción
switch ($Action) {
    "create" {
        Create-Backup -BackupReason $Reason
    }
    "restore" {
        Restore-Backup -BackupName $BackupName
    }
    default {
        Write-Host "Uso: .\backup.ps1 [create|restore] [motivo]" -ForegroundColor White
        exit 1
    }
}
