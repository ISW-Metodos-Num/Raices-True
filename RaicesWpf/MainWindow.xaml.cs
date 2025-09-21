using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using NCalc;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RaicesWpf
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<IterStep> _steps = new();
        private readonly ObservableCollection<ComparacionRow> _comparacion = new();


       
        private NCalc.Expression? _exprF;   // f(x)
        private NCalc.Expression? _exprDf;  // f'(x) opcional
        

        public MainWindow()
        {
            InitializeComponent();
            dgIteraciones.ItemsSource = _steps;
            dgComparativa.ItemsSource = _comparacion;
            cbEjemplos.SelectedIndex = 0;
        }

        private void Metodo_Checked(object sender, RoutedEventArgs e)
        {
            if (txtSugerencias == null) return;
            if (rbBiseccion.IsChecked == true)
                txtSugerencias.Text = "Biseccion: requiere xi, xf con cambio de signo. Ej: f en [0,1].";
            else if (rbReglaFalsa.IsChecked == true)
                txtSugerencias.Text = "Regla Falsa: requiere xi, xf con cambio de signo. Ej: g en [-3,-2] o [2,3].";
            else if (rbSecante.IsChecked == true)
                txtSugerencias.Text = "Secante: usa xi=x0, xf=x1 (no requiere cambio de signo).";
            else if (rbNewton.IsChecked == true)
                txtSugerencias.Text = "Newton: usa xi=x0 (ignora xf). Si f'(x) se deja vacio, usa derivada numerica.";
        }

        private void CbEjemplos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            switch (cbEjemplos.SelectedIndex)
            {
                case 0: // f
                    txtFuncion.Text = "4*x**3-6*x**2+7*x-2.3";
                    txtDerivada.Text = "12*x**2-12*x+7";
                    txtXi.Text = "0";
                    txtXf.Text = "1";
                    rbBiseccion.IsChecked = true;
                    break;
                case 1: // g
                    txtFuncion.Text = "x**2*Sqrt(Abs(Cos(x)))-5";
                    txtDerivada.Text = "";
                    txtXi.Text = "2";
                    txtXf.Text = "3";
                    rbReglaFalsa.IsChecked = true;
                    break;
                default: // personalizada
                    break;
            }
        }

        private void Calcular_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Normaliza potencia: ^ -> ** (para usuarios acostumbrados a ^)
                string fxText = NormalizeExpressionText(txtFuncion.Text);
                _exprF = new NCalc.Expression(fxText);
                _exprF.Parameters["pi"] = Math.PI;
                _exprF.Parameters["e"] = Math.E;

                _exprDf = null;
                if (!string.IsNullOrWhiteSpace(txtDerivada.Text))
                {
                    string dfxText = NormalizeExpressionText(txtDerivada.Text);
                    _exprDf = new NCalc.Expression(dfxText);
                    _exprDf.Parameters["pi"] = Math.PI;
                    _exprDf.Parameters["e"] = Math.E;
                }

                double xi = ParseDouble(txtXi.Text);
                double xf = ParseDouble(txtXf.Text);
                double eamax = ParseDouble(txtEamax.Text);
                if (double.IsNaN(xi) || (double.IsNaN(xf) && rbNewton.IsChecked != true) || double.IsNaN(eamax))
                    throw new ArgumentException("Verifica xi, xf y eamax.");
                if (chkEaPorc.IsChecked == true) eamax /= 100.0;
                if (eamax <= 0) throw new ArgumentException("ea máx debe ser > 0.");

                _steps.Clear();

                // Delegados de evaluación
                double EvalF(double x) => EvalExpression(_exprF!, x);
                double EvalDf(double x) => (_exprDf is null) ? DerivadaNumerica(EvalF, x) : EvalExpression(_exprDf!, x);

                double raiz;
                if (rbBiseccion.IsChecked == true)
                {
                    if (EvalF(xi) * EvalF(xf) > 0) throw new ArgumentException("Biseccion: no hay cambio de signo en [xi, xf].");
                    raiz = BiseccionConTabla(EvalF, xi, xf, eamax);
                    txtResumenMetodo.Text = "Metodo: Bisección";
                }
                else if (rbReglaFalsa.IsChecked == true)
                {
                    if (EvalF(xi) * EvalF(xf) > 0) throw new ArgumentException("Regla Falsa: no hay cambio de signo en [xi, xf].");
                    raiz = ReglaFalsaConTabla(EvalF, xi, xf, eamax);
                    txtResumenMetodo.Text = "Metodo: Regla Falsa";
                }
                else if (rbSecante.IsChecked == true)
                {
                    raiz = SecanteConTabla(EvalF, xi, xf, eamax);
                    txtResumenMetodo.Text = "Metodo: Secante (xi=x0, xf=x1)";
                }
                else // Newton
                {
                    raiz = NewtonConTabla(EvalF, EvalDf, xi, eamax);
                    txtResumenMetodo.Text = "Metodo: Newton-Raphson (xi=x0)";
                }

                lblRaiz.Text = raiz.ToString("F6", CultureInfo.InvariantCulture);
                lblFRAIZ.Text = EvalF(raiz).ToString("F6", CultureInfo.InvariantCulture);
                lblIter.Text = _steps.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string NormalizeExpressionText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            // Reemplaza '^' por '**' para exponentes
            s = s.Replace("^", "**");
            // Normaliza 'ln(' a 'Log(' (log natural) si el usuario lo escribió
            s = s.Replace("ln(", "Log(", StringComparison.OrdinalIgnoreCase);
            return s;
        }

        private static double EvalExpression(NCalc.Expression expr, double x)
        {
            expr.Parameters["x"] = x;
            var v = expr.Evaluate();
            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private void Limpiar_Click(object sender, RoutedEventArgs e)
        {
            _steps.Clear();
            lblRaiz.Text = lblFRAIZ.Text = lblIter.Text = "";
            txtResumenMetodo.Text = "";
        }

        private static double ParseDouble(string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)) return v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return double.NaN;
        }

        // ------------------------------
        // BISECCIÓN
        // ------------------------------
        private double BiseccionConTabla(Func<double, double> f, double xi, double xf, double eamax)
        {
            double fxi = f(xi);
            double fxf = f(xf);
            double xr = xi, xrPrev = xr;
            const int iterMax = 1000;
            int it = 0;

            while (it < iterMax)
            {
                xrPrev = xr;
                xr = 0.5 * (xi + xf);
                double fxr = f(xr);
                double ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr);

                _steps.Add(new IterStep
                {
                    N = it + 1,
                    Xi = xi,
                    FXi = fxi,
                    Xf = xf,
                    FXu = fxf,
                    Xr = xr,
                    FXr = fxr,
                    EaPercent = double.IsInfinity(ea) ? double.NaN : ea * 100.0
                });

                it++;
                if (fxr == 0.0 || ea <= eamax) break;

                if (fxi * fxr < 0) { xf = xr; fxf = fxr; }
                else { xi = xr; fxi = fxr; }
            }
            return xr;
        }

        // ------------------------------
        // REGLA FALSA
        // ------------------------------
        private double ReglaFalsaConTabla(Func<double, double> f, double xi, double xf, double eamax)
        {
            double fxi = f(xi);
            double fxf = f(xf);
            double xr = xi, xrPrev = xr;
            const int iterMax = 1000;
            int it = 0;

            while (it < iterMax)
            {
                xrPrev = xr;
                xr = xf - fxf * (xi - xf) / (fxi - fxf);
                double fxr = f(xr);
                double ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr);

                _steps.Add(new IterStep
                {
                    N = it + 1,
                    Xi = xi,
                    FXi = fxi,
                    Xf = xf,
                    FXu = fxf,
                    Xr = xr,
                    FXr = fxr,
                    EaPercent = double.IsInfinity(ea) ? double.NaN : ea * 100.0
                });

                it++;
                if (fxr == 0.0 || ea <= eamax) break;

                if (fxi * fxr < 0) { xf = xr; fxf = fxr; }
                else { xi = xr; fxi = fxr; }
            }
            return xr;
        }

        // ------------------------------
        // SECANTE
        // ------------------------------
        private double SecanteConTabla(Func<double, double> f, double x0, double x1, double eamax)
        {
            double f0 = f(x0);
            double f1 = f(x1);
            const int iterMax = 1000;
            int it = 0;
            double xr = x1, xrPrev = xr;

            while (it < iterMax)
            {
                xrPrev = xr;
                double denom = (f0 - f1);
                if (denom == 0) throw new InvalidOperationException("Secante: f(x0) = f(x1), division por cero");
                xr = x1 - f1 * (x0 - x1) / denom;
                double fr = f(xr);
                double ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr);

                _steps.Add(new IterStep
                {
                    N = it + 1,
                    Xi = x0,
                    FXi = f0,
                    Xf = x1,
                    FXu = f1,
                    Xr = xr,
                    FXr = fr,
                    EaPercent = double.IsInfinity(ea) ? double.NaN : ea * 100.0
                });

                it++;
                if (fr == 0.0 || ea <= eamax) break;

                // shift
                x0 = x1; f0 = f1;
                x1 = xr; f1 = fr;
            }
            return xr;
        }

        // ------------------------------
        // NEWTON-RAPHSON
        // ------------------------------
        private double NewtonConTabla(Func<double, double> f, Func<double, double> df, double x0, double eamax)
        {
            const int iterMax = 1000;
            int it = 0;
            double xr = x0, xrPrev = xr;

            while (it < iterMax)
            {
                double fx = f(xr);
                double dfx = df(xr);
                if (dfx == 0) throw new InvalidOperationException("Newton: f'(x) = 0. Intenta otro x0 o ingresa f'(x).");

                xrPrev = xr;
                double xrNext = xr - fx / dfx;
                double fr = f(xrNext);
                double ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xrNext - xrPrev) / xrNext);

                _steps.Add(new IterStep
                {
                    N = it + 1,
                    Xi = xr,
                    FXi = fx,
                    Xf = double.NaN,
                    FXu = double.NaN,
                    Xr = xrNext,
                    FXr = fr,
                    EaPercent = double.IsInfinity(ea) ? double.NaN : ea * 100.0
                });

                it++;
                xr = xrNext;
                if (fr == 0.0 || ea <= eamax) break;
            }

            return xr;
        }

        private static double DerivadaNumerica(Func<double, double> f, double x)
        {
            // Diferencias centrales
            double h = Math.Pow(2, -26);
            return (f(x + h) - f(x - h)) / (2 * h);
        }


        private ComparacionRow RunBiseccion(Func<double, double> f, double xi, double xf, double eamax)
        {
            if (f(xi) * f(xf) > 0) throw new ArgumentException("No hay cambio de signo en [xi, xf].");
            double fxi = f(xi), fxf = f(xf);
            double xr = xi, xrPrev = xr;
            double ea = double.PositiveInfinity;
            int it = 0, iterMax = 1000;

            while (it < iterMax)
            {
                xrPrev = xr;
                xr = 0.5 * (xi + xf);
                double fxr = f(xr);
                ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr) * 100.0; // %
                it++;
                if (fxr == 0.0 || ea <= eamax * 100.0) break;
                if (fxi * fxr < 0) { xf = xr; fxf = fxr; } else { xi = xr; fxi = fxr; }
            }

            return new ComparacionRow { Metodo = "Bisección", Iteraciones = it, Raiz = xr, YRaiz = f(xr), ErrorAprox = double.IsInfinity(ea) ? double.NaN : ea, Estado = "OK" };
        }

        private ComparacionRow RunReglaFalsa(Func<double, double> f, double xi, double xf, double eamax)
        {
            if (f(xi) * f(xf) > 0) throw new ArgumentException("No hay cambio de signo en [xi, xf].");
            double fxi = f(xi), fxf = f(xf);
            double xr = xi, xrPrev = xr;
            double ea = double.PositiveInfinity;
            int it = 0, iterMax = 1000;

            while (it < iterMax)
            {
                xrPrev = xr;
                xr = xf - fxf * (xi - xf) / (fxi - fxf);
                double fxr = f(xr);
                ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr) * 100.0;
                it++;
                if (fxr == 0.0 || ea <= eamax * 100.0) break;
                if (fxi * fxr < 0) { xf = xr; fxf = fxr; } else { xi = xr; fxi = fxr; }
            }

            return new ComparacionRow { Metodo = "Regla falsa", Iteraciones = it, Raiz = xr, YRaiz = f(xr), ErrorAprox = double.IsInfinity(ea) ? double.NaN : ea, Estado = "OK" };
        }

        private ComparacionRow RunSecante(Func<double, double> f, double x0, double x1, double eamax)
        {
            double f0 = f(x0), f1 = f(x1);
            double xr = x1, xrPrev = xr;
            double ea = double.PositiveInfinity;
            int it = 0, iterMax = 1000;

            while (it < iterMax)
            {
                xrPrev = xr;
                double denom = (f0 - f1);
                if (denom == 0) throw new InvalidOperationException("f(x0) = f(x1), division por cero.");
                xr = x1 - f1 * (x0 - x1) / denom;
                double fr = f(xr);
                ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xr - xrPrev) / xr) * 100.0;
                it++;
                if (fr == 0.0 || ea <= eamax * 100.0) break;
                x0 = x1; f0 = f1; x1 = xr; f1 = fr;
            }

            return new ComparacionRow { Metodo = "Secante", Iteraciones = it, Raiz = xr, YRaiz = f(xr), ErrorAprox = double.IsInfinity(ea) ? double.NaN : ea, Estado = "OK" };
        }

        private ComparacionRow RunNewton(Func<double, double> f, Func<double, double> df, double x0, double eamax)
        {
            int it = 0, iterMax = 1000;
            double xr = x0, xrPrev = xr, ea = double.PositiveInfinity;

            while (it < iterMax)
            {
                double fx = f(xr);
                double dfx = df(xr);
                if (dfx == 0) throw new InvalidOperationException("f'(x) = 0. Intenta otro x0 o ingresa f'(x).");
                xrPrev = xr;
                double xrNext = xr - fx / dfx;
                ea = (it == 0) ? double.PositiveInfinity : Math.Abs((xrNext - xrPrev) / xrNext) * 100.0;
                it++;
                xr = xrNext;
                if (Math.Abs(f(xr)) == 0.0 || ea <= eamax * 100.0) break;
            }

            return new ComparacionRow { Metodo = "Newton - Raphson", Iteraciones = it, Raiz = xr, YRaiz = f(xr), ErrorAprox = double.IsInfinity(ea) ? double.NaN : ea, Estado = "OK" };
        }


        public class IterStep
        {
            public int N { get; set; }
            public double Xi { get; set; }
            public double FXi { get; set; }
            public double Xf { get; set; }
            public double FXu { get; set; }
            public double Xr { get; set; }
            public double FXr { get; set; }
            public double EaPercent { get; set; }
        }

        public class ComparacionRow
        {
            public string Metodo { get; set; } = "";
            public int Iteraciones { get; set; }
            public double Raiz { get; set; }
            public double YRaiz { get; set; }
            public double ErrorAprox { get; set; } // último ea (%) de ese método
            public string Estado { get; set; } = "OK";

            private bool _isBest;
            public bool IsBest
            {
                get => _isBest;
                set { if (_isBest != value) { _isBest = value; OnPropertyChanged(); } }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        }


        private void Comparar_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                // Construye f y (opcional) f' con NCalc, igual que en Calcular_Click
                string fxText = NormalizeExpressionText(txtFuncion.Text);
                var exprF = new NCalc.Expression(fxText);
                exprF.Parameters["pi"] = Math.PI;
                exprF.Parameters["e"] = Math.E;

               

                NCalc.Expression? exprDf = null;
                if (!string.IsNullOrWhiteSpace(txtDerivada.Text))
                {
                    string dfxText = NormalizeExpressionText(txtDerivada.Text);
                    exprDf = new NCalc.Expression(dfxText);
                    exprDf.Parameters["pi"] = Math.PI;
                    exprDf.Parameters["e"] = Math.E;
                }

                double EvalF(double x) { exprF.Parameters["x"] = x; return Convert.ToDouble(exprF.Evaluate(), CultureInfo.InvariantCulture); }
                double EvalDf(double x)
                {
                    if (exprDf == null) return DerivadaNumerica(EvalF, x);
                    exprDf.Parameters["x"] = x; return Convert.ToDouble(exprDf.Evaluate(), CultureInfo.InvariantCulture);
                }

                double xi = ParseDouble(txtXi.Text);
                double xf = ParseDouble(txtXf.Text);
                double eamax = ParseDouble(txtEamax.Text);
                if (double.IsNaN(xi) || (double.IsNaN(xf) && rbNewton.IsChecked != true) || double.IsNaN(eamax))
                    throw new ArgumentException("Verifica xi, xf y eamax.");
                if (chkEaPorc.IsChecked == true) eamax /= 100.0;
                if (eamax <= 0) throw new ArgumentException("ea max debe ser > 0.");

                _comparacion.Clear();

                // Ejecuta cada método si “aplica”; si no, muestra el motivo en Estado
                void TryAdd(Func<ComparacionRow> run)
                {
                    try { _comparacion.Add(run()); }
                    catch (Exception ex) { _comparacion.Add(new ComparacionRow { Metodo = run().Metodo, Estado = ex.Message }); }
                }

                // Bisección
                try { _comparacion.Add(RunBiseccion(EvalF, xi, xf, eamax)); }
                catch (Exception ex) { _comparacion.Add(new ComparacionRow { Metodo = "Bisección", Estado = ex.Message }); }

                // Regla Falsa
                try { _comparacion.Add(RunReglaFalsa(EvalF, xi, xf, eamax)); }
                catch (Exception ex) { _comparacion.Add(new ComparacionRow { Metodo = "Regla falsa", Estado = ex.Message }); }

                // Secante (xi=x0, xf=x1)
                try { _comparacion.Add(RunSecante(EvalF, xi, xf, eamax)); }
                catch (Exception ex) { _comparacion.Add(new ComparacionRow { Metodo = "Secante", Estado = ex.Message }); }

                // Newton (xi=x0)
                try { _comparacion.Add(RunNewton(EvalF, EvalDf, xi, eamax)); }
                catch (Exception ex) { _comparacion.Add(new ComparacionRow { Metodo = "Newton - Raphson", Estado = ex.Message }); }

                // Elige “mejor” entre los OK: menos iteraciones; tie-break: |f(raíz)| y luego ea
                var ok = _comparacion.Where(r => r.Estado == "OK").ToList();
                if (ok.Count > 0)
                {
                    var best = ok
                        .OrderBy(r => r.Iteraciones)
                        .ThenBy(r => Math.Abs(r.YRaiz))
                        .ThenBy(r => Math.Abs(r.ErrorAprox))
                        .First();

                    foreach (var r in _comparacion) r.IsBest = false;
                    best.IsBest = true;
                    txtMejor.Text = $"Mejor metodo: {best.Metodo} (iteraciones: {best.Iteraciones})";
                }
                else
                {
                    txtMejor.Text = "Mejor metodo: N/A (ninguno aplico con los datos ingresados).";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

