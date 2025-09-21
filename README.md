# Raíces de Funciones — WPF con NCalc

App WPF (.NET 8) que calcula raíces con **Bisección**, **Regla Falsa**, **Secante** y **Newton–Raphson**.
Permite que el usuario defina `f(x)` (y opcionalmente `f'(x)` para Newton) usando **NCalc**.

## Instrucciones rápidas
1. **Restaura el paquete**:
   ```bash
   dotnet restore
   ```
2. **Ejecuta**:
   ```bash
   dotnet run --project ./RaicesWpf/RaicesWpf.csproj
   ```
3. **Publica EXE** (self-contained, win-x64):
   ```bash
   dotnet publish ./RaicesWpf/RaicesWpf.csproj -c Release -r win-x64 --self-contained true      -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
   ```

## Sintaxis (NCalc)
- Variable: `x`
- **Potencia**: usa `**` o `Pow(a,b)` (si escribes `^`, la app lo convierte a `**` automáticamente).
- Funciones: `Sin, Cos, Tan, Asin, Acos, Atan, Sqrt, Abs, Exp, Log, Log10, Pow`.
- Constantes: `pi`, `e`.

### Ejemplos
- `4*x**3 - 6*x**2 + 7*x - 2.3`
- `Pow(x,2) * Sqrt(Abs(Cos(x))) - 5`
- `Exp(-x) - x`  (Newton opcional: `f'(x) = -Exp(-x) - 1`)

## Tolerancia
- `ea máx` como **fracción** (`0.001` = **0.1%**) o marca “Interpretar ea como %”.
