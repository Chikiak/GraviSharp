# GraviSharp

Simulación de gravitación 2D con field de estrellas y renderizado GPU-friendly, construida en C# 14 / .NET 11 Preview con NativeAOT target y Raylib-cs 8.x.

## Concepto

GraviSharp es una simulación de física gravitacional en 2D que visualiza la interacción entre cuerpos celestes. El proyecto explora técnicas de renderizado de alto rendimiento y cálculo paralelo usando tecnologías modernas de .NET.

## Características principales

- **Starfield determinista**: 10,000 partículas generadas con semilla fija para reproducibilidad
- **Renderizado GPU-friendly**: Batch rendering con `DrawPixel` y hot-path sin allocations
- **NativeAOT**: Compilación nativa para binarios standalone sin runtime (.NET 11 Preview)
- **SIMD Ready**: Preparado para kernels vectorizados en fases posteriores
- **Trimming agresivo**: Reducción máxima de tamaño de binario final

## Stack tecnológico

- **.NET 11 Preview** (C# 14, NativeAOT)
- **Raylib-cs 8.x** (bindings a raylib 5.x nativo)
- **Server GC** para throughput en simulación

## Build & Run

```bash
# Restaurar + build release
dotnet build -c Release

# Publicar self-contained JIT (fallback funcional)
dotnet publish -c Release -r win-x64 --self-contained
.\bin\Release\net11.0\win-x64\publish\GraviSharp.exe
```

## Estado

Proyecto en desarrollo activo. Ver `plans/` para roadmap detallado por fases.

## Licencia

MIT