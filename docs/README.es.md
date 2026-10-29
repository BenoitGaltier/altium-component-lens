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

Todos los grupos duplicados se muestran en una sola página, sin botón Siguiente. Las URL, los tokens, el idioma y la última comparación se guardan automáticamente en `%LOCALAPPDATA%\ComponentLens\state.dat`, cifrado por Windows para tu cuenta. Al reiniciar se restauran las conexiones y la comparación fechada sin contactar con Altium. Ejecuta un nuevo análisis para actualizar los datos. Al actualizar desde 1.0.x hay que introducir los tokens una vez, porque las versiones anteriores no los guardaban.

## Criterios y cobertura

Se comparan los Part Choices y los parámetros de fabricante/MPN, incluidas las parejas numeradas. Solo se usa la última revisión de cada componente. La normalización ignora mayúsculas y espacios exteriores y unifica los espacios del fabricante; conserva la puntuación y los espacios interiores del MPN. Desactívala para comparar valores originales exactos. No se fusionan automáticamente los alias de fabricantes ni las equivalencias eléctricas.

Las categorías proceden de Altium y usan su ruta completa. Un grupo sigue visible si al menos uno de sus componentes pertenece a una categoría seleccionada, aunque en el otro workspace se haya clasificado de forma distinta. Los filtros se aplican después de leer toda la biblioteca; no reducen las llamadas a la API. Los nombres de las categorías se muestran como están definidos en los workspaces.

**Entre A y B** muestra identidades compartidas. **Dentro de A** y **Dentro de B** muestran identidades utilizadas por varios componentes del mismo workspace, incluidas las compartidas con el otro. **Todos** reúne estos grupos. Los contadores resumen todo el análisis; la tabla y las exportaciones siguen los filtros.

Las identidades ausentes o no utilizables se indican como incidencias. Si falla la lectura de parámetros, el informe es **PARCIAL** y puede omitir duplicados. Los fallos de red, autenticación o cursores impiden generar el informe. La cobertura depende de los permisos del token; no hay un único instante de captura para ambas bibliotecas. Evita modificarlas durante el análisis.

La paginación de la API sigue los cursores hasta la última página. Las filas repetidas se deduplican por identificador. Si los componentes distintos superan un total de API estable, una segunda lectura completa verifica los identificadores y metadatos. Dos lecturas idénticas convierten la diferencia en información, sin una falsa advertencia de informe parcial. Los inventarios incompletos o cambiantes, las verificaciones fallidas y los parámetros no leídos conservan el estado **PARCIAL**, con un solo aviso. El diagnóstico y el JSON mantienen los detalles. Los cursores ausentes, repetidos o sin progreso siguen deteniendo el análisis.

Los tokens guardados nunca se devuelven al navegador ni aparecen en exportaciones o registros. Deja vacío el campo de token para reutilizar el guardado para la misma URL. Cambiar la URL requiere otro token e invalida la comparación anterior; renovar el token para la misma URL conserva la comparación. Un informe parcial sigue siendo parcial al restaurarlo. La demo no se guarda como resultado real.

**Datos locales** muestra la carpeta y permite borrar conexiones, comparación y copia de seguridad cifrada. Solo una instancia puede usar esa carpeta a la vez. Los archivos alterados o indescifrables se conservan y se indican. La aplicación escucha únicamente en `127.0.0.1` y no modifica datos de Altium.

El ejecutable incluye .NET y puede extraer archivos nativos al directorio temporal en el primer inicio. La distribución no está firmada. Para compilar y verificar, consulta el [README principal](../README.md) y las [notas de validación](VALIDATION.md).
