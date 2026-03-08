# Sistema de Carga Dinámica de Chunks 3x3

## Descripción General
Se ha implementado un sistema de carga dinámica de chunks que mantiene una cuadrícula 3x3 alrededor del jugador en todo momento.

## Componentes Principales

### DynamicChunkLoader
- **Ubicación**: `scripts/systems/DynamicChunkLoader.cs`
- **Función**: Gestiona la carga/descarga automática de chunks
- **Características**:
  - Tracking continuo de posición del jugador
  - Carga asíncrona de chunks faltantes
  - Descarga automática de chunks lejanos
  - Cuadrícula 3x3 configurable (GRID_RADIUS = 1)

### Integración con GameWorldInitializer
- El sistema se inicializa automáticamente en el paso 12 del proceso
- Se conecta con TerrainManager y PlayerController
- Inicialización con posición actual del jugador

## Funcionamiento

### 1. Detección de Movimiento
- El sistema monitorea la posición del jugador cada 0.5 segundos
- Convierte coordenadas del mundo a coordenadas de chunk
- Detecta cuando el jugador cambia de chunk

### 2. Carga de Chunks
- Cuando el jugador cambia de chunk, se determina la nueva cuadrícula 3x3 requerida
- Se cargan en paralelo los chunks que faltan
- Se utilizan los métodos existentes de TerrainManager para carga/generación

### 3. Descarga de Chunks
- Se identifican chunks cargados que ya no están en la cuadrícula 3x3
- Se liberan recursos llamando a `TerrainManager.UnloadChunk()`
- Se emiten señales para notificar eventos de carga/descarga

## Configuración

### Parámetros Principales
```csharp
private const int GRID_RADIUS = 1; // 3x3 grid (1 chunk a cada lado + centro)
private const int CHUNK_SIZE = 100; // metros
private const float UPDATE_INTERVAL = 0.5f; // segundos entre actualizaciones
```

### Personalización
- Para cambiar el tamaño de la cuadrícula: modificar `GRID_RADIUS`
- Para ajustar frecuencia de actualización: modificar `UPDATE_INTERVAL`

## Eventos y Señales

### ChunkLoaded
```csharp
[Signal]
public delegate void ChunkLoadedEventHandler(Vector2I chunkPosition);
```

### ChunkUnloaded
```csharp
[Signal]
public delegate void ChunkUnloadedEventHandler(Vector2I chunkPosition);
```

## Métodos Útiles

### Forzar Actualización
```csharp
await _dynamicChunkLoader.ForceUpdateAsync();
```

### Obtener Estadísticas
```csharp
var (loaded, required, currentChunk) = _dynamicChunkLoader.GetStats();
```

### Descargar Todo
```csharp
_dynamicChunkLoader.UnloadAllChunks();
```

## Optimizaciones Implementadas

### 1. Carga Asíncrona
- Los chunks se cargan en paralelo usando `Task.WhenAll`
- No bloquea el hilo principal del juego

### 2. Control de Actualizaciones
- Evita actualizaciones simultáneas con `_isUpdating`
- Intervalo configurable para balance rendimiento/precisión

### 3. Integración con Sistema Existente
- Reutiliza TerrainManager para carga/generación
- Compatible con sistema de sub-chunks optimizado
- Mantiene compatibilidad con colisiones y renderizado

## Flujo de Operación

1. **Inicialización**: El sistema detecta la posición inicial del jugador y carga la cuadrícula 3x3
2. **Monitoreo**: Cada 0.5 segundos verifica si el jugador cambió de chunk
3. **Actualización**: Si hay cambio, calcula nueva cuadrícula y gestiona carga/descarga
4. **Optimización**: Solo carga chunks faltantes y descarga los innecesarios

## Compatibilidad

### Con Sistema de Sub-chunks
- Los chunks cargados mantienen su sistema de sub-chunks
- El culling por distancia sigue funcionando normalmente
- No interfiere con la optimización de renderizado

### Con Sistema de Persistencia
- Los chunks se guardan/generan usando el sistema existente
- No afecta el guardado de posición del jugador
- Compatible con mundos existentes

## Rendimiento

### Memoria
- Máximo 9 chunks cargados simultáneamente (3x3)
- Cada chunk tiene 100 sub-chunks de 10x10m
- Descarga automática de chunks no utilizados

### Procesamiento
- Actualizaciones cada 0.5 segundos (configurable)
- Carga asíncrona para evitar parones
- Operaciones paralelas cuando es posible

## Pruebas Recomendadas

### 1. Movimiento Continuo
- Caminar en línea recta para verificar carga/descarga
- Observar logs para confirmar operaciones

### 2. Movimiento Diagonal
- Probar cambios de chunk en diagonal
- Verificar que no haya chunks faltantes

### 3. Teleportación
- Usar comandos de teleport para prueba rápida
- Verificar respuesta del sistema a cambios grandes

### 4. Estadísticas
- Monitorear `GetStats()` durante el juego
- Verificar que `loaded` nunca exceda 9 chunks

## Solución de Problemas

### Chunks No Cargan
- Verificar que TerrainManager esté inicializado
- Comprobar que PlayerController esté conectado
- Revisar logs de errores de carga

### Performance Issues
- Aumentar `UPDATE_INTERVAL` para reducir frecuencia
- Verificar que no haya múltiples instancias del sistema
- Monitorear uso de memoria

### Chunks No Se Descargan
- Verificar que el jugador realmente se aleje
- Comprobar límites de la cuadrícula 3x3
- Revisar logs de descarga

## Futuras Mejoras

### 1. Cuadrícula Configurable
- Permitir diferentes tamaños (5x5, 7x7)
- Configuración por calidad gráfica

### 2. Pre-carga Predictiva
- Anticipar movimiento del jugador
- Cargar chunks en dirección de movimiento

### 3. Niveles de Detalle
- Chunks lejanos con menor detalle
- Sistema LOD para mayor rendimiento

## Estado Actual
- ✅ Sistema implementado y funcional
- ✅ Integrado con GameWorldInitializer
- ✅ Compatible con sistemas existentes
- ✅ Listo para pruebas en juego
