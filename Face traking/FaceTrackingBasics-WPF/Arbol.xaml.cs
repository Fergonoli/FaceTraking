using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FaceTrackingBasics
{
    /// <summary>
    /// Lógica de interacción para Arbol.xaml
    /// </summary>
    public partial class Arbol : Window
    {
        public string res = "";

        public Arbol()
        {
            InitializeComponent();
        }

        private void buttonAceptar_Click(object sender, RoutedEventArgs e)
        {
            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_1ArbolTopSkull7personas.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_2ArbolNariz17Personas.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_3ºArbolTopSkull31PersonasSinPoda.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_4ºArbolTopSkullConPoda31personas.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_5ºArbolNarizConPoda22Personas.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_6ºArbolCaractTopSkull31Personas.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            //Comprobar que checkbox están activos
            if ((bool)this.checkBox_7ºArbolCaractTopSkull31PersonasNorm.IsChecked)
            {
                res = res + "1";
            }
            else
            {
                res = res + "0";
            }

            Close();
        }
    }
}
