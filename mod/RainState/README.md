# Sistema de Configuración de Ciclos RainState

Este es el nuevo sistema de gestión de paletas basado en ciclos. Las configuraciones se cargan al inicio del mod y no soportan hot updates por rendimiento.

## Estructura de Carpetas

```
mod/
└── RainState/
    ├── NombreRegion/
    │   ├── 01_dia.json          # Configuración para el primer ciclo
    │   ├── 02_tarde.json        # Configuración para el segundo ciclo
    │   ├── 03_noche.json        # Configuración para el tercer ciclo
    │   └── NombreRoom/          # Carpeta para configuraciones específicas de una room
    │       ├── 01_config.json
    │       └── 02_config.json
    └── OtraRegion/
        └── 01_config.json
```

## Convención de Nombres

### Archivos de Configuración

Todos los archivos JSON deben seguir el formato: `[NN]_[nombre].json`

- **NN**: Los primeros **2 caracteres** deben ser números que determinan el índice del ciclo (ej: `01`, `02`, `03`, `10`, `99`)
- **Nombre**: Descripción opcional del ciclo (ej: `dia`, `tarde`, `noche`)

Ejemplos válidos:
- `01_dia.json` → Índice 1
- `02_tarde.json` → Índice 2
- `03_noche.json` → Índice 3
- `10_special.json` → Índice 10
- `99_final.json` → Índice 99

⚠️ **Importante**: Los primeros 2 caracteres del nombre determinan la posición en la lista de ciclos.

### Carpetas de Región

El nombre de la carpeta debe coincidir con el nombre de la región en el juego (ej: `SU`, `HI`, `CC`, etc.)

### Carpetas de Room

Si deseas configuraciones específicas para una room, crea una carpeta dentro de la región con el nombre **completo** de la room (ej: `SU_A01`, `HI_B02`).

## Formato del Archivo JSON

Cada archivo JSON debe contener un **objeto** con la siguiente estructura:

```json
{
  "palette": [0, 5, 10, 15],
  "time": [0.0, 0.3, 0.6, 1.0],
  "effectA": ["#FFA500", "#FFD700", "#FFFFFF"],
  "effectATime": [0.0, 0.5, 1.0],
  "effectB": ["#87CEEB", "#4682B4"],
  "effectBTime": [0.0, 1.0]
}
```

### Propiedades

**Base Palette (Requerido):**
- **`palette`**: Lista de IDs de paleta que se usarán en la región
- **`time`**: Lista de valores decimales (0-1) que definen cuándo cada paleta toma efecto
  - El primer valor debe ser siempre 0

**Effect Palettes (Opcional):**
- **`effectA`**: Lista de colores hexadecimales (ej: `"#FF5500"` o `"FF5500"`)
- **`effectATime`**: Lista de valores decimales (0-1) que definen cuándo aplicar cada color
  - El primer valor debe ser siempre 0
  
- **`effectB`**: Lista de colores hexadecimales (ej: `"#00AAFF"` o `"00AAFF"`)
- **`effectBTime`**: Lista de valores decimales (0-1) que definen cuándo aplicar cada color
  - El primer valor debe ser siempre 0

**Formato de colores hexadecimales:**
- Formatos soportados: `"#RRGGBB"`, `"RRGGBB"`, `"#RGB"`, o `"RGB"`
- Ejemplos:
  - `"#FF0000"` = Rojo
  - `"#00FF00"` = Verde
  - `"#0000FF"` = Azul
  - `"#FFA500"` = Naranja

## Prioridad de Configuraciones

1. **Room específica**: Si existe una configuración para una room específica, tiene prioridad.
2. **Región**: Si no hay configuración de room, se usa la configuración de la región.

## Orden de Ciclos

El orden de los ciclos se determina por los **primeros 2 caracteres numéricos** del nombre del archivo:
- `01_dia.json` → Índice 1
- `02_tarde.json` → Índice 2
- `03_noche.json` → Índice 3

Los archivos se cargan y se colocan en la lista según este índice numérico.

## Ejemplo Completo

### Estructura
```
RainState/
└── SU/
    ├── 01_morning.json
    ├── 02_afternoon.json
    ├── 03_evening.json
    └── SU_A01/
        ├── 01_special_morning.json
        └── 02_special_evening.json
```

### Resultado
- La región `SU` tendrá 3 configuraciones de ciclo (morning, afternoon, evening)
- La room `SU_A01` tendrá 2 configuraciones específicas que sobrescriben las de la región
