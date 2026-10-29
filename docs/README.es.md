# Component Lens — Español

Aplicación portátil para Windows que identifica componentes duplicados entre dos workspaces de Altium 365 mediante **Manufacturer + Manufacturer Part Number**. Interfaz en español, francés e inglés.

## Inicio

Descarga el ZIP `win-x64` de Releases, extráelo y ejecuta **ComponentLens.exe**. Se abre tu navegador. No hace falta instalar .NET, Node.js ni Altium Designer. Mantén abierta la ventana del ejecutable; **Salir** o cerrar esta ventana detiene la aplicación.

Requisitos: Windows 10/11 x64, navegador moderno, conexión a Altium 365 y un token de API con acceso de lectura a la biblioteca de cada workspace.

1. Configura y conecta los workspaces A y B con sus URL y tokens.
2. Selecciona **Buscar duplicados**.
3. Filtra por las categorías reales de Altium: resistencias, condensadores u otros tipos presentes en tus bibliotecas.
4. Abre cada referencia para revisar los componentes, revisiones, categorías y fuentes de identidad.
5. Exporta los grupos filtrados a CSV o JSON. La tarjeta **Datos incompletos** permite revisar y exportar las incidencias.

**Explorar demo** utiliza datos ficticios identificados y no accede a ningún workspace.

## Criterios y cobertura

Se comparan los Part Choices y los parámetros de fabricante/MPN, incluidas las parejas numeradas. Solo se usa la última revisión de cada componente. La normalización ignora mayúsculas y espacios exteriores y unifica los espacios del fabricante; conserva la puntuación y los espacios interiores del MPN. Desactívala para comparar valores originales exactos. No se fusionan automáticamente los alias de fabricantes ni las equivalencias eléctricas.

Las categorías proceden de Altium y usan su ruta completa. Un grupo sigue visible si al menos uno de sus componentes pertenece a una categoría seleccionada, aunque en el otro workspace se haya clasificado de forma distinta. Los filtros se aplican después de leer toda la biblioteca; no reducen las llamadas a la API. Los nombres de las categorías se muestran como están definidos en los workspaces.

**Entre A y B** muestra identidades compartidas. **Dentro de A** y **Dentro de B** muestran identidades utilizadas por varios componentes del mismo workspace, incluidas las compartidas con el otro. **Todos** reúne estos grupos. Los contadores resumen todo el análisis; la tabla y las exportaciones siguen los filtros.

Las identidades ausentes o no utilizables se indican como incidencias. Si falla la lectura de parámetros, el informe es **PARCIAL** y puede omitir duplicados. Los fallos de red, autenticación o cursores impiden generar el informe. La cobertura depende de los permisos del token; no hay un único instante de captura para ambas bibliotecas. Evita modificarlas durante el análisis.

Desde la versión 1.0.1, la paginación sigue los cursores hasta la última página. Las filas repetidas se deduplican por identificador de componente. Un total de API distinto o cambiante genera un informe **PARCIAL** con todos los componentes leídos, sin bloquear la comparación. El diagnóstico muestra filas, componentes distintos, repeticiones, páginas y totales anunciados; la exportación JSON conserva estos datos. Un componente repetido con valores distintos también hace que el informe sea parcial. Los cursores ausentes, repetidos o una paginación sin progreso siguen deteniendo el análisis.

Los tokens solo permanecen en memoria y no se guardan en exportaciones, registros ni almacenamiento del navegador. Cierra el ejecutable para descartarlos. La aplicación escucha únicamente en `127.0.0.1` y no modifica datos de Altium. Solo se recuerdan los nombres, URL de los workspaces y el idioma.

El ejecutable incluye .NET y puede extraer archivos nativos al directorio temporal en el primer inicio. La distribución no está firmada. Para compilar y verificar, consulta el [README principal](../README.md) y las [notas de validación](VALIDATION.md).
