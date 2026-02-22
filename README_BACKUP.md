# Sistema de Backups Automáticos - Wild

## 🚀 Características

- **Backups automáticos** antes de cada compilación
- **Restauración con un clic** 
- **Mantiene últimos 10 backups**
- **Metadatos JSON** con timestamp y motivo
- **Integración VS Code** con atajos de teclado

## 📁 Estructura

```
wild/
├── backups/
│   ├── 20250222_184530_manual/
│   ├── 20250222_185000_before_build/
│   └── 20250222_185230_feature/
├── backup.ps1              # Script principal
├── .vscode/
│   ├── tasks.json          # Tareas de VS Code
│   └── keybindings.json   # Atajos de teclado
└── README_BACKUP.md       # Este archivo
```

## 🎮 Uso

### Atajos de Teclado (VS Code)

- **Ctrl+Shift+B** - Crear backup manual
- **Ctrl+Shift+R** - Restaurar último backup
- **Ctrl+Shift+L** - Listar backups disponibles

### Comandos PowerShell

```powershell
# Crear backup manual
.\backup.ps1 create "motivo_del_backup"

# Restaurar último backup
.\backup.ps1 restore

# Restaurar backup específico
.\backup.ps1 restore "20250222_184530_manual"

# Listar todos los backups
.\backup.ps1 list
```

### Tareas VS Code (Ctrl+Shift+P)

- **Create Backup** - Crea backup manual
- **Restore Last Backup** - Restaura último backup
- **List Backups** - Muestra todos los backups

## 🔄 Flujo Automático

1. **Antes de compilar** → Backup automático con motivo "before_build"
2. **Compilación exitosa** → Backup automático con motivo "after_build"
3. **Manual** → Backup con motivo que tú definas
4. **Rotación** → Mantiene solo los 10 más recientes

## 📋 Archivos Críticos Respaldados

- `scenes/game_world.tscn` - Escena principal del juego
- `scripts/GameWorld.cs` - Lógica del mundo
- `scripts/autoload/GameFlow.cs` - Flujo del juego
- `scripts/NewGameMenu.cs` - Menú nueva partida
- `project.godot` - Configuración del proyecto
- `Wild.csproj` - Proyecto .NET

## 🛠️ En Caso de Problemas

### Si algo va mal:

1. **Para el desarrollo** inmediatamente
2. **Ctrl+Shift+R** para restaurar último backup
3. **Verifica** que el juego funcione
4. **Continúa** desde punto estable

### Si un backup está corrupto:

1. **Ctrl+Shift+L** para listar backups
2. **Elige uno anterior** al problema
3. **Restaura manualmente**:
   ```powershell
   .\backup.ps1 restore "nombre_del_backup"
   ```

## 📊 Metadatos

Cada backup incluye `metadata.json` con:
- **timestamp** - Fecha y hora exacta
- **reason** - Motivo del backup
- **files** - Lista de archivos respaldados
- **godot_version** - Versión de Godot usada

## 🔧 Personalización

### Cambiar número de backups:

Edita `backup.ps1` línea:
```powershell
$MaxBackups = 10  # Cambia este número
```

### Añadir archivos críticos:

Edita `backup.ps1` línea:
```powershell
$CriticalFiles = @(
    "scenes/game_world.tscn",
    "scripts/GameWorld.cs",
    # Añade aquí más archivos
)
```

## 🎯 Mejores Prácticas

1. **Antes de cambios grandes** → Backup manual con motivo descriptivo
2. **Después de hitos** → Backup para marcar progreso
3. **Regularmente** → Cada 30 minutos de desarrollo
4. **Antes de experimentar** → Siempre crea un backup

## 🚨 Recuperación de Desastres

Si todo falla completamente:

1. **Abre la carpeta `backups/`**
2. **Busca el backup más reciente funcional**
3. **Copia manualmente** los archivos al proyecto principal
4. **Verifica** con `dotnet build` y ejecución

---

**¡Ahora tienes seguridad total para desarrollar sin miedo a romper algo!** 🎮✨
