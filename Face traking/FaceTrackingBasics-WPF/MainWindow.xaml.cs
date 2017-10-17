// -----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using System.Drawing;
    using System.IO;
    using System.Globalization;
    using Microsoft.Kinect.Toolkit.FaceTracking;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Point = System.Windows.Point;
    using System.Threading;
    using System.Windows.Media.Media3D;


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /////////////////////////////////////////////////////////////////////////////////
        ////////////  VARIABLES DEL PROGRAMA PRINCIPAL  /////////////////////////////////

        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();
        private WriteableBitmap colorImageWritableBitmap;
        private byte[] colorImageData;
        private ColorImageFormat currentColorImageFormat = ColorImageFormat.Undefined;

        //Variable para conocer si la BBDD (en formato txt) esta creada en la carpeta correspondiente
        public bool BBDDExiste = false;

        //Variable para controlar cuando se esta grabando y cuando no
        public bool video = false;

        //Variable para ajustar la grabacion a 1 fotograma por segundo, para evitar errores de ejecución
        public bool fotograma = true;

        //Variable que controla que emocion esta clasificandose
        public int emocion = 0;

        //Variable para ...... 
        public int contador = 0;

        //Variable para saber con que emocion se va a etiquetar la grabacion de los fotogramas
        public string EtiquetaEmocion = "Neutra";

        //Variable que contiene la lista de puntos del rostro, debidamente etiquetada por sus nombres y identificadores
        public List<PuntoNombrado> ListaPuntos = new List<PuntoNombrado>();

        //Vector para contabilizar cada una de las emociones
        public int[,] Emociones = new int[7,2];

        //Vector para contabilizar cada una de las emociones emitidas y hacer estadísticas de manera Total, desde el momento que la Kinect empieza a recibir datos
        public int[,] EmocionesTotales = new int[7, 2];

        //Vector para contabilizar cada una de las emociones emitidas y hacer estadística, desde el momento que el que se esta grabando y se tiene una etiqueta para comparar
        public int[,] EmocionesTotalesEtiquetada = new int[7, 2];

        //Variable para evitar que una etiqueta ya clasificada se cuente mas de una vez, hasta que se obtenga la siguiente hasta varios fotogramas etiquetados despues.
        public bool TotalContado = false;

        //Variable para indicar en que momento se comienza a contabilizar las emociones, para hacer las estadisticas
        public bool Comenzar_A_Contar = false;

        //Variable para marcar cuantas comprobaciones se han hecho
        public int ContadorComprobaciones = 0;

        //Variable para conocer que arboles se van a emplear en las clasificaciones
        public int[] ArbolesActivos = new int[7];
        
        //Variables para controlar los FPS
        public string FechaYHoraActualReal = null;
        public string FechaYHoraActualTotal = null;
        public int FramePerSecondReales = 0;
        public int FramePerSecondTotales = 1;



        public MainWindow()
        {
            InitializeComponent();

            //Iniciar ArbolesActivos a 0, es decir que ninguno esta activado para clasificar
            this.DesactivarArboles(ArbolesActivos);

            //Generar Lista de puntos 
            this.GenerarListaDePuntos();

            //Inicializamos el archivo de TXT para el arff
            this.IniciarArchivosDeTexto();


            var faceTrackingViewerBinding = new Binding("Kinect") { Source = sensorChooser };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.KinectProperty, faceTrackingViewerBinding);
            sensorChooser.KinectChanged += SensorChooserOnKinectChanged;           
            sensorChooser.Start();
           

        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {           
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
            KinectSensor newSensor = kinectChangedEventArgs.NewSensor;
            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= KinectSensorOnAllFramesReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }

            if (newSensor != null)
            {
                try
                {
                    newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    newSensor.SkeletonStream.Enable();
                    newSensor.AllFramesReady += KinectSensorOnAllFramesReady;

                    fotograma = true;
                }
                catch (InvalidOperationException)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur, say, in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                }
            }
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            sensorChooser.Stop();
            faceTrackingViewer.Dispose();
        }

        private void KinectSensorOnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            //Rutas para los logs de datos y sumario de resultados
            string FechaYHora = DateTime.Now.Hour.ToString() + "h" + DateTime.Now.Minute.ToString() + "'" + DateTime.Now.Second.ToString() + "''";
            string NombreFichero = "Imagen_" + FechaYHora + EtiquetaEmocion + ".bmp";

            //Calculo de los FPS
            //Si la fecha es la misma, se está en el mismo segundo
            if (FechaYHora.Equals(FechaYHoraActualTotal))
            {
                //Se incrementa el FPS
                FramePerSecondTotales++;
            }
            //Si no es la misma es porque hay nuevo segundo
            else
            {
                //Se actualiza el label de los frames
                float latencia = (float) FramePerSecondReales / FramePerSecondTotales;
                float porcentaje = (1 - latencia) * 100;

                Tiempo.Content = "Latencia:  " + porcentaje.ToString() + "%";

                //Se devuelve a 0 los frames
                FramePerSecondTotales = 1;

                //Se actualiza el instante actual
                FechaYHoraActualTotal = FechaYHora;
            }

            //AQUI PUEDE INCLUIRSE LOS CAMBIOS DE IMAGENES SEGUN LA EMOCION
            //Comprobacion de la emocion
            //  1 -> Alegria
            //  2 -> Tristeza
            //  3 -> Sorpresa
            //  4 -> Enfado
            //  5 -> Asco
            //  6 -> Miedo
            //  7 -> Neutra

            //Se inicializan los resultados por defecto de los arboles
            int[] Emocion_Arbol1_TopSkull_7Personas = {10,0};
            int[] Emocion_Arbol2_Nariz_17Personas = {10,0};
            int[] Arbol31personasTopSkullSinPoda = { 10, 0 };
            int[] Arbol31personasTopSkullConPoda = { 10, 0 };
            int[] Arbol22personasNarizConPoda = { 10, 0 };
            int[] Arbol31personasCaracteristicasTopSkull = { 10, 0 };
            int[] Arbol31personasCaracteristicasTopSkullNormalizado = { 10, 0 };
            //LUGAR PARA EL SIGUIENTE ARBOL
            //
            //
      

            List<Point3D> PuntosCara = new List<Point3D>();
            PuntosCara = this.faceTrackingViewer.DevolverPuntos3D();

            //Si el 1º arbol esta activo se lanza su clasificador
            if (this.ArbolesActivos[0] == 1)
            {
                Emocion_Arbol1_TopSkull_7Personas = this.ComprobarEmocion_1Arbol_TopSkull_7Personas(PuntosCara);
            }

            //Si el 2º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[1] == 1)
            {
                Emocion_Arbol2_Nariz_17Personas = this.ComprobarEmocion_2Arbol_Nariz_17Personas(PuntosCara);
            }

            //Si el 3º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[2] == 1)
            {
                //Aqui es donde se implementarán los if-then-else del arbol de decision
                //Si no hay puntos terminamos la ejecución
                if (PuntosCara.Count == 0)
                {
                    this.emocion = 10;

                }
                else
                {
                    //Realizamos el traslado de los puntos, con TopSkull
                    this.TraslacionTopSkull(PuntosCara);

                    //Se realiza la conversion a array de doubles
                    double[] puntos = this.conversion(ListaPuntos);

                    //Se lanza a ejecucion el arbol automatica llamando a la clase
                    Arbol31personasTopSkull ejec = new Arbol31personasTopSkull();
                    Arbol31personasTopSkullSinPoda[0] = (int)ejec.classify(puntos) + 1;
                    Arbol31personasTopSkullSinPoda[1] = 0;
                }
            }

            //Si el 4º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[3] == 1)
            {
                //Aqui es donde se implementarán los if-then-else del arbol de decision
                //Si no hay puntos terminamos la ejecución
                if (PuntosCara.Count == 0)
                {
                    this.emocion = 10;

                }
                else
                {
                    //Realizamos el traslado de los puntos, con TopSkull
                    this.TraslacionTopSkull(PuntosCara);

                    //Se realiza la conversion a array de doubles
                    double[] puntos = this.conversion(ListaPuntos);

                    //Se lanza a ejecucion el arbol automatica llamando a la clase
                    Arbol31personasTopSkullConPoda ejec = new Arbol31personasTopSkullConPoda();
                    Arbol31personasTopSkullConPoda[0] = (int)ejec.classify(puntos) + 1;
                    Arbol31personasTopSkullConPoda[1] = 0;
                }
            }

            //Si el 5º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[4] == 1)
            {
                //Aqui es donde se implementarán los if-then-else del arbol de decision
                //Si no hay puntos terminamos la ejecución
                if (PuntosCara.Count == 0)
                {
                    this.emocion = 10;

                }
                else
                {
                    //Realizamos el traslado de los puntos, con Nariz
                    this.TraslacionNariz(PuntosCara);

                    //Se realiza la conversion a array de doubles
                    double[] puntos = this.conversion(ListaPuntos);

                    //Se lanza a ejecucion el arbol automatica llamando a la clase
                    Arbol22personasNarizConPoda ejec = new Arbol22personasNarizConPoda();
                    Arbol22personasNarizConPoda[0] = (int)ejec.classify(puntos) + 1;
                    Arbol22personasNarizConPoda[1] = 0;
                }
            }

            //Si el 6º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[5] == 1)
            {
                //Aqui es donde se implementarán los if-then-else del arbol de decision
                //Si no hay puntos terminamos la ejecución
                if (PuntosCara.Count == 0)
                {
                    this.emocion = 10;

                }
                else
                {
                    //Realizamos el traslado de los puntos, con TopSkull
                    this.TraslacionTopSkull(PuntosCara);

                    //Se crea un objeto de tipo caracteristicas, donde se calculan las distancias
                    Caracteristicas Caract = new Caracteristicas(ListaPuntos);

                    //Se realiza la conversion a array de doubles
                    double[] puntos = Caract.DevolverCaracteristicas();

                    //Se lanza a ejecucion el arbol automatica llamando a la clase
                    ArbolCaracteristicasTopSkull31personas ejec = new ArbolCaracteristicasTopSkull31personas();
                    Arbol31personasCaracteristicasTopSkull[0] = (int)ejec.classify(puntos) + 1;
                    Arbol31personasCaracteristicasTopSkull[1] = 0;
                }
            }

            //Si el 7º arbol  esta activo se lanza su clasificador
            if (this.ArbolesActivos[6] == 1)
            {
                //Aqui es donde se implementarán los if-then-else del arbol de decision
                //Si no hay puntos terminamos la ejecución
                if (PuntosCara.Count == 0)
                {
                    this.emocion = 10;

                }
                else
                {
                    //Realizamos el traslado de los puntos, con TopSkull
                    this.TraslacionTopSkull(PuntosCara);

                    //Realizamos la normalizacion de los datos
                    this.NormalizarPuntos(ListaPuntos);

                    //Se crea un objeto de tipo caracteristicas, donde se calculan las distancias
                    Caracteristicas Caract = new Caracteristicas(ListaPuntos);

                    //Se realiza la conversion a array de doubles
                    double[] puntos = Caract.DevolverCaracteristicas();

                    //Se lanza a ejecucion el arbol automatica llamando a la clase
                    ArbolCaracteristicasTopSkull31personasNormalizado ejec = new ArbolCaracteristicasTopSkull31personasNormalizado();
                    Arbol31personasCaracteristicasTopSkullNormalizado[0] = (int)ejec.classify(puntos) + 1;
                    Arbol31personasCaracteristicasTopSkullNormalizado[1] = 0;
                }
            } 
            //LUGAR PARA EL SIGUIENTE ARBOL
            //
            //



            if (this.Comenzar_A_Contar)
            {
                //Si el 1º arbol esta activo se lanza su sumario
                if (this.ArbolesActivos[0] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 1 
                    string result1 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Emocion_Arbol1_TopSkull_7Personas[0] + " con exp: " + Emocion_Arbol1_TopSkull_7Personas[1];

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result1);
                    }
                }

                //Si el 2º arbol esta activo se lanza su sumario
                if (this.ArbolesActivos[1] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result2 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Emocion_Arbol2_Nariz_17Personas[0] + " con exp: " + Emocion_Arbol2_Nariz_17Personas[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result2);
                    }
                }

                //Si el 3º arbol  esta activo se lanza su clasificador
                if (this.ArbolesActivos[2] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result3 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Arbol31personasTopSkullSinPoda[0] + " con exp: " + Arbol31personasTopSkullSinPoda[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result3);
                    }
                }

                //Si el 4º arbol  esta activo se lanza su clasificador
                if (this.ArbolesActivos[3] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result3 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Arbol31personasTopSkullConPoda[0] + " con exp: " + Arbol31personasTopSkullConPoda[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result3);
                    }
                }

                //Si el 5º arbol  esta activo se lanza su clasificador
                if (this.ArbolesActivos[4] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result3 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Arbol22personasNarizConPoda[0] + " con exp: " + Arbol22personasNarizConPoda[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result3);
                    }
                }

                //Si el 6º arbol  esta activo se lanza su clasificador
                if (this.ArbolesActivos[5] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result3 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Arbol31personasCaracteristicasTopSkull[0] + " con exp: " + Arbol31personasCaracteristicasTopSkull[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result3);
                    }
                }
                //Si el 7º arbol  esta activo se lanza su clasificador
                if (this.ArbolesActivos[6] == 1)
                {
                    //Guardar el sumario de la comprobacion del arbol 2 
                    string result3 = "Instante de recogida: " + FechaYHora + " Emocion clasificada: " + Arbol31personasCaracteristicasTopSkullNormalizado[0] + " con exp: " + Arbol31personasCaracteristicasTopSkullNormalizado[1]; ;

                    using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", true))
                    {
                        file1.WriteLine(result3);
                    }
                }
                //LUGAR PARA EL SIGUIENTE ARBOL
                //
                //


            }

            //Se mira con cuantos arboles se estan trabajando para ver cuantas comprobaciones se deben esperar son 10 fotogramas por cada arbol
            // 1 arbol -> 10 comprobaciones || 2 arbol -> 20 comprobaciones (10 por cada arbol) || ETC....
            int NumCompro = 0;
            for (int i = 0; i < this.ArbolesActivos.Length; i++)
            {
                NumCompro = NumCompro + this.ArbolesActivos[i];
            }

            //Resultados del arbol 1
            if (Emocion_Arbol1_TopSkull_7Personas[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Emocion_Arbol1_TopSkull_7Personas[0] - 1,0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Emocion_Arbol1_TopSkull_7Personas[0] - 1, 1] = this.Emociones[Emocion_Arbol1_TopSkull_7Personas[0] - 1, 1] + Emocion_Arbol1_TopSkull_7Personas[1];

                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10*NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }

            //Resultados del arbol 2
            if (Emocion_Arbol2_Nariz_17Personas[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Emocion_Arbol2_Nariz_17Personas[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Emocion_Arbol2_Nariz_17Personas[0] - 1, 1] = this.Emociones[Emocion_Arbol2_Nariz_17Personas[0] - 1, 1] + Emocion_Arbol2_Nariz_17Personas[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10*NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }

            //Prueba del arbol 3
            if (Arbol31personasTopSkullSinPoda[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Arbol31personasTopSkullSinPoda[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Arbol31personasTopSkullSinPoda[0] - 1, 1] = this.Emociones[Arbol31personasTopSkullSinPoda[0] - 1, 1] + Arbol31personasTopSkullSinPoda[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10 * NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }

            //Prueba del arbol 4
            if (Arbol31personasTopSkullConPoda[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Arbol31personasTopSkullConPoda[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Arbol31personasTopSkullConPoda[0] - 1, 1] = this.Emociones[Arbol31personasTopSkullConPoda[0] - 1, 1] + Arbol31personasTopSkullConPoda[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10 * NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }

            //Prueba del arbol 5
            if (Arbol22personasNarizConPoda[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Arbol22personasNarizConPoda[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Arbol22personasNarizConPoda[0] - 1, 1] = this.Emociones[Arbol22personasNarizConPoda[0] - 1, 1] + Arbol22personasNarizConPoda[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10 * NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }

            //Prueba del arbol 6
            if (Arbol31personasCaracteristicasTopSkull[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Arbol31personasCaracteristicasTopSkull[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Arbol31personasCaracteristicasTopSkull[0] - 1, 1] = this.Emociones[Arbol31personasCaracteristicasTopSkull[0] - 1, 1] + Arbol31personasCaracteristicasTopSkull[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10 * NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            }  

            //Prueba del arbol 7
            if (Arbol31personasCaracteristicasTopSkullNormalizado[0] != 10)
            {
                //El indice del vector se mueve de 0 a 6 y las emociones de 1 a 7, hay que restarle 1
                this.Emociones[Arbol31personasCaracteristicasTopSkullNormalizado[0] - 1, 0]++;

                //Sumar la Exp recibida para ese nodo
                this.Emociones[Arbol31personasCaracteristicasTopSkullNormalizado[0] - 1, 1] = this.Emociones[Arbol31personasCaracteristicasTopSkullNormalizado[0] - 1, 1] + Arbol31personasCaracteristicasTopSkullNormalizado[1];


                //Se comprueba que se tienen 10 lecturas para devolver un resultado
                if (this.Cantidad_Vector_Emociones(this.Emociones) == 10 * NumCompro)
                {
                    //Se comprueba cual de las emociones ha sido mas etiquetada y se le suma +1 de nuevo
                    emocion = this.Emocion_Escogida() + 1;

                    //Se reinician los valores del Contador de emociones
                    this.Vaciar_Vector_Emociones(this.Emociones);

                    this.ContadorComprobaciones = 0;

                    this.TotalContado = true;
                }
            } 
            //LUGAR PARA EL SIGUIENTE ARBOL
            //
            //



            //Se muestran las estadisticas de cada emocion
            this.ImprimirEstadisiticas();


            //Colocar imagen de Alegria
            if (emocion == 1)
            {
                //Ocultamos las demas
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de alegria
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[0,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[0,0]++;
                    }

                    this.TotalContado = false;
                }
            }

            //Colocar imagen de Tristeza
            if (emocion == 2)
            {
                //Ocultamos las demas
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Tristeza
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[1,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[1,0]++;
                    }

                    this.TotalContado = false;
                }
            }

            //Colocar imagen de Sorpresa
            if (emocion == 3)
            {
                //Ocultamos las demas
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Sorpresa
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[2,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[2,0]++;
                    }

  
                    this.TotalContado = false;
                }
            }

            //Colocar imagen de Enfado
            if (emocion == 4)
            {
                //Ocultamos las demas
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Enfado
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[3,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[3,0]++;
                    }

                    this.TotalContado = false;
                }
            }

            //Colocar imagen de Asco
            if (emocion == 5)
            {
                //Ocultamos las demas
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Asco
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[4,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[4,0]++;
                    }

                    this.TotalContado = false;
                }
            }

            //Colocar imagen de Miedo
            if (emocion == 6)
            {
                //Ocultamos las demas
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.image_Neutra.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Miedo
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[5,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[5,0]++;
                    }

                    this.TotalContado = false;
                }
            }


            //Colocar imagen de Neutra
            if (emocion == 7)
            {
                //Ocultamos las demas
                this.ImagenEmocionAlegria.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionEnfado.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionSorpresa.Visibility = System.Windows.Visibility.Hidden;
                this.ImagnEmocionTristeza.Visibility = System.Windows.Visibility.Hidden;
                this.ImagenEmocionAsco.Visibility = System.Windows.Visibility.Hidden;
                this.imageEmocionMiedo.Visibility = System.Windows.Visibility.Hidden;

                //Activamos la de Neutra
                this.image_Neutra.Visibility = System.Windows.Visibility.Visible;

                //Si se esta grabando, porque se esta etiquetando un video
                if (TotalContado)
                {
                    this.EmocionesTotales[6,0]++;

                    if (video)
                    {
                        this.EmocionesTotalesEtiquetada[6,0]++;
                    }

                    this.TotalContado = false;
                }
            }

            //Grabación de fotogramas
            if (video && fotograma)
            {
                //Guardamos el Fotograma
                //Almacenamos la imagen
                CreateThumbnail(NombreFichero, this.colorImageWritableBitmap.Clone());



                //Despues realizamos el almacenaje de los puntos
                //Almacenamos los datos de la matriz 

                string textPuntosCaraTopSkull = "" + FechaYHora + EtiquetaEmocion+",";
                string textPuntosCaraNariz = "" + FechaYHora + EtiquetaEmocion + ",";

                //Condicion para que no falle si no hay personas recogiendo datos
                if (PuntosCara.Count > 71 )
                {
                    //Se cogen los puntos iniciales de la matriz de la cara para el traslado a la posicion (0,0)
                    float X_initTopSkull = (float)PuntosCara[0].X;
                    float Y_initTopSkull = (float)PuntosCara[0].Y;
                    float Z_initTopSkull = (float)PuntosCara[0].Z;

                    //Se cogen los puntos iniciales de la matriz de la cara para el traslado a la posicion (0,0)
                    float X_initCaraNari = (float)PuntosCara[94].X;
                    float Y_initCaraNari = (float)PuntosCara[94].Y;
                    float Z_initCaraNari = (float)PuntosCara[94].Z;

                    //En este primer for recorremos la lista de puntos de la matriz de la cara que genera la kinect
                    for (int i = 0; i < PuntosCara.Count; i++)
                    {

                        // Con el segundo recorremos la lista de puntos importantes que he creado para discriminar los puntos que
                        // se seleccionarán y se guardaran en el fichero TXT con su informacion importante

                        foreach (PuntoNombrado PuntoAct in ListaPuntos)
                        {
                            if (i == PuntoAct.DevolverInt())
                            {

                                //Trasladamos los puntos a un lugar estandar
                                float nX = (float)PuntosCara[i].X - X_initTopSkull;
                                float nY = (float)PuntosCara[i].Y - Y_initTopSkull;
                                float nZ = (float)PuntosCara[i].Z - Z_initTopSkull;

                                //Para devolver bien los datos las partes enteras y decimales, deben estar separada por .
                                string X = nX.ToString();
                                string Y = nY.ToString();
                                string Z = nZ.ToString();

                                X = X.Replace(",", ".");
                                Y = Y.Replace(",", ".");
                                Z = Z.Replace(",", ".");

                                textPuntosCaraTopSkull = textPuntosCaraTopSkull + X + "," + Y + "," + Z + ",";
                            }

                            if (i == PuntoAct.DevolverInt())
                            {

                                //Trasladamos los puntos a un lugar estandar
                                float nX = (float)PuntosCara[i].X - X_initCaraNari;
                                float nY = (float)PuntosCara[i].Y - Y_initCaraNari;
                                float nZ = (float)PuntosCara[i].Z - Z_initCaraNari;

                                //Para devolver bien los datos las partes enteras y decimales, deben estar separada por .
                                string X = nX.ToString();
                                string Y = nY.ToString();
                                string Z = nZ.ToString();

                                X = X.Replace(",", ".");
                                Y = Y.Replace(",", ".");
                                Z = Z.Replace(",", ".");

                                textPuntosCaraNariz = textPuntosCaraNariz + X + "," + Y + "," + Z + ",";
                            }

                        }
                    }

                    textPuntosCaraTopSkull = textPuntosCaraTopSkull + EtiquetaEmocion;
                    textPuntosCaraNariz = textPuntosCaraNariz + EtiquetaEmocion;
                }
                  
                //Guardamos los datos de ese fotograma al lado de la images
                System.IO.File.WriteAllText(@"C:\Base De Datos\Text" + FechaYHora + EtiquetaEmocion + "TopSkull.txt", textPuntosCaraTopSkull);
                System.IO.File.WriteAllText(@"C:\Base De Datos\Text" + FechaYHora + EtiquetaEmocion + "Nariz.txt", textPuntosCaraNariz);

                //Por ultimo incluimos los datos de la imagen en la BBDD       
                //Sacamos los valores de los parametros en el formato de texto
                //String donde se va a cargar toda la base de datos

                //Almacenamos la nueva
                string DatosTopSkull = "" + textPuntosCaraTopSkull;
                string DatosCaraNariz = "" + textPuntosCaraNariz;

                using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\BBDDEmocionesTopSkull.txt", true))
                {
                    file1.WriteLine(DatosTopSkull);
                }

                using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\BBDDEmocionesNariz.txt", true))
                {
                    file1.WriteLine(DatosCaraNariz);
                }

                fotograma = false;             
            }

            LabelEtiquetado.Content ="Etiquetando como: " +EtiquetaEmocion;

            using (var colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame())
            {
                if (colorImageFrame == null)
                {
                    return;
                }

                // Make a copy of the color frame for displaying.
                var haveNewFormat = this.currentColorImageFormat != colorImageFrame.Format;
                if (haveNewFormat)
                {
                    this.currentColorImageFormat = colorImageFrame.Format;
                    this.colorImageData = new byte[colorImageFrame.PixelDataLength];
                    this.colorImageWritableBitmap = new WriteableBitmap(
                        colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                    ColorImage.Source = this.colorImageWritableBitmap;

                }

                colorImageFrame.CopyPixelDataTo(this.colorImageData);
                this.colorImageWritableBitmap.WritePixels(
                    new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height),
                    this.colorImageData,
                    colorImageFrame.Width * Bgr32BytesPerPixel,
                    0);
                fotograma = true;
            }


            //Calculo de los FPS
            //Si la fecha es la misma, se está en el mismo segundo
            if (FechaYHora.Equals(FechaYHoraActualReal))
            {
                //Se incrementa el FPS
                FramePerSecondReales++;
            }
            //Si no es la misma es porque hay nuevo segundo
            else
            {
                //Se actualiza el label de los frames
                FPS.Content = "FPS:  " + FramePerSecondReales.ToString();

                //Se devuelve a 0 los frames
                FramePerSecondReales = 0;

                //Se actualiza el instante actual
                FechaYHoraActualReal = FechaYHora;
            }

        }

        //Metodo para la Traslacion de los puntos al punto TopSkull como coordenadas (0,0,0)
        private void TraslacionTopSkull(List<Point3D> lista)
        {

            //Puntos de referencia para hacer el traslado TopSkull, posicion 0
            float X_init = (float)lista[0].X;
            float Y_init = (float)lista[0].Y;
            float Z_init = (float)lista[0].Z;


            //En este primer for recorremos la lista de puntos de la matriz de la cara
            for (int i = 0; i < lista.Count; i++)
            {

                // Con el segundo recorremos la lista de puntos importantes que he creado para discriminar los puntos que
                // se seleccionarán y se guardaran en el fichero TXT con su informacion importante
                foreach (PuntoNombrado PuntoAct in ListaPuntos)
                {
                    if (i == PuntoAct.DevolverInt())
                    {

                        //Trasladamos los puntos a un lugar estandar
                        float X = (float)lista[i].X - X_init;
                        float Y = (float)lista[i].Y - Y_init;
                        float Z = (float)lista[i].Z - Z_init;

                        //Lo guardamos a la lista de puntos
                        PuntoAct.ModificarPoint3DData(X, Y, Z);
                    }
                }
            }
        }

        //Metodo para la Traslacion de los puntos al punto Nariz como coordenadas (0,0,0)
        private void TraslacionNariz(List<Point3D> lista)
        {

            //Puntos de referencia para hacer el traslado Nariz, posicion 94
            float X_init = (float)lista[94].X;
            float Y_init = (float)lista[94].Y;
            float Z_init = (float)lista[94].Z;


            //En este primer for recorremos la lista de puntos de la matriz de la cara
            for (int i = 0; i < lista.Count; i++)
            {

                // Con el segundo recorremos la lista de puntos importantes que he creado para discriminar los puntos que
                // se seleccionarán y se guardaran en el fichero TXT con su informacion importante
                foreach (PuntoNombrado PuntoAct in ListaPuntos)
                {
                    if (i == PuntoAct.DevolverInt())
                    {

                        //Trasladamos los puntos a un lugar estandar
                        float X = (float)lista[i].X - X_init;
                        float Y = (float)lista[i].Y - Y_init;
                        float Z = (float)lista[i].Z - Z_init;

                        //Lo guardamos a la lista de puntos
                        PuntoAct.ModificarPoint3DData(X, Y, Z);
                    }
                }
            }
        }

        //Metodo para Normalizar las coordenandas de cada mascara
        private void NormalizarPuntos(List<PuntoNombrado> lista)
        {

            //Se toma los puntos XYZ del ojo Izq, Punto: OuterCornerOfLeftEye  Indentificador: 27
            float X_OjoIzq = (float)lista[27].DevolverPunto().X;
            float Y_OjoIzq = (float)lista[27].DevolverPunto().Y;
            float Z_OjoIzq = (float)lista[27].DevolverPunto().Z;

            //Se toma los puntos XYZ del ojo Dch, Punto: OuterCornerOfRigthEye  Indentificador: 53
            float X_OjoDch = (float)lista[10].DevolverPunto().X;
            float Y_OjoDch = (float)lista[10].DevolverPunto().Y;
            float Z_OjoDch = (float)lista[10].DevolverPunto().Z;

            //Se calcula la distancia euclidea de estos puntos       
            float VdisX = X_OjoIzq - X_OjoDch;
            float VdisY = Y_OjoIzq - Y_OjoDch;
            float VdisZ = Z_OjoIzq - Z_OjoDch;

            float Dis = (float) Math.Sqrt( Math.Pow(VdisX, 2) + Math.Pow( VdisY, 2) + Math.Pow( VdisZ, 2));


                
            // Con el segundo recorremos la lista de puntos importantes que he creado para discriminar los puntos que
            // se seleccionarán y se guardaran en el fichero TXT con su informacion importante
            foreach (PuntoNombrado PuntoAct in lista)
            {
                //Trasladamos los puntos a un lugar estandar
                float X = (float)PuntoAct.DevolverPunto().X / Dis;
                float Y = (float)PuntoAct.DevolverPunto().Y / Dis;
                float Z = (float)PuntoAct.DevolverPunto().Z / Dis;
                      
                //Lo guardamos a la lista de puntos      
                PuntoAct.ModificarPoint3DData(X, Y, Z);
            }
        }
        
        //Lista de emociones
        //  0 -> Neutra
        //  1 -> Alegria
        //  2 -> Tristeza
        //  3 -> Sorpresa
        //  4 -> Enfado
        //  5 -> Asco
        //  6 -> Miedo
        public int[] ComprobarEmocion_1Arbol_TopSkull_7Personas(List<Point3D> PuntosCara )
        {
            //Aqui es donde se implementarán los if-then-else del arbol de decision
            //Si no hay puntos terminamos la ejecución
            if (PuntosCara.Count == 0)
            {
                int[] a = { 10, 0 };
                return a;
            }

            //Realizamos el traslado de los puntos, con TopSkull
            this.TraslacionTopSkull(PuntosCara);

            //LeftofRightEyebrowY -> Punto 7, ID 17 coordenada Y
            if ((ListaPuntos[7].DevolverPunto().Y <= -0.075904) && (ListaPuntos[7].DevolverInt() == 17))
            {
                //LeftSideOfCheekZ   -> Punto 32, ID 63 coordenada Z
                if ((ListaPuntos[32].DevolverPunto().Z <= 0.051199) && (ListaPuntos[32].DevolverInt() == 63))
                {
                    //OutSideRightCornerMouthY   -> Punto 16, ID 31 coordenada Y
                    if ((ListaPuntos[16].DevolverPunto().Y <= -0.151921) && (ListaPuntos[16].DevolverInt() == 31))
                    {
                        //RightSideOfChinX   -> Punto 15, ID 30 coordenada X
                        if ((ListaPuntos[15].DevolverPunto().X <= -0.05495) && (ListaPuntos[15].DevolverInt() == 30))
                        {
                            //MiddleTopOfLEftEyebrowX   -> Punto 23, ID 49 coordenada X
                            if ((ListaPuntos[23].DevolverPunto().X <= 0.031541) && (ListaPuntos[23].DevolverInt() == 49))
                            {
                                //Devolver miedo, con 53|1 de exp
                                int [] a = {6,52};
                                return a;
                            }
                            else
                            //Else del MiddleTopOfLEftEyebrowX
                            {
                                //TopRigthForeheadY   -> Punto 1, ID 1 coordenada Y
                                if ((ListaPuntos[1].DevolverPunto().Y <= -0.034271) && (ListaPuntos[1].DevolverInt() == 1))
                                {
                                    //LeftBottomUpperLipY   -> Punto 47, ID 82 coordenada Y
                                    if ((ListaPuntos[47].DevolverPunto().Y <= -0.157076) && (ListaPuntos[47].DevolverInt() == 82))
                                    {
                                        //AboveOneFourthRightEyelidY   -> Punto 65, ID 103 coordenada Y
                                        if ((ListaPuntos[65].DevolverPunto().Y <= -0.110753) && (ListaPuntos[65].DevolverInt() == 103))
                                        {
                                            //OuterCornerOfRightEyeY   -> Punto 10, ID 20 coordenada X
                                            if ((ListaPuntos[10].DevolverPunto().Y <= -0.11354) && (ListaPuntos[10].DevolverInt() == 20))
                                            {
                                                //Devolver Alegria, con 24 de exp
                                                int[] a = {1, 24};
                                                return a;
                                            }
                                            else
                                            //Else del OuterCornerOfRightEyeY
                                            {
                                                //Devolver miedo, con 2 de exp
                                                int[] a = { 6, 2 };
                                                return a;
                                            }
                                        }
                                        else
                                        //Else del AboveOneFourthRightEyelidY
                                        {
                                            //RightCornerMouthX   -> Punto 54, ID 89 coordenada X
                                            if ((ListaPuntos[54].DevolverPunto().X <= 0.010913) && (ListaPuntos[54].DevolverInt() == 89))
                                            {
                                                //TopRightForeheadY   -> Punto 1, ID 1 coordenada X
                                                if ((ListaPuntos[1].DevolverPunto().Y <= -0.036316) && (ListaPuntos[1].DevolverInt() == 1))
                                                {
                                                    //AboveMidUpperRightEyelidY   -> Punto 9, ID 19 coordenada X
                                                    if ((ListaPuntos[9].DevolverPunto().Y <= -0.103716) && (ListaPuntos[9].DevolverInt() == 19))
                                                    {
                                                        //Devolver Tristeza, con 25 de exp
                                                        int[] a = { 2, 25 };
                                                        return a;
                                                    }
                                                    else
                                                    //Else del AboveMidUpperRightEyelidY
                                                    {
                                                        //Devolver Sorpresa, con 18 de exp
                                                        int[] a = { 3, 18 };
                                                        return a;
                                                    }

                                                }
                                                else
                                                //Else del TopRightForeheadY
                                                {
                                                    //OuterCornerOfRightEyeX   -> Punto 10, ID 20 coordenada X
                                                    if ((ListaPuntos[10].DevolverPunto().X <= -0.053216) && (ListaPuntos[10].DevolverInt() == 20))
                                                    {
                                                        //Devolver Alegria, con 8|1 de exp
                                                        int[] a = { 1, 7 };
                                                        return a;
                                                    }
                                                    else
                                                    //Else del OuterCornerOfRightEyeX
                                                    {
                                                        //RightOfLeftEyebrowY   -> Punto 24, ID 50 coordenada X
                                                        if ((ListaPuntos[24].DevolverPunto().X <= -0.101184) && (ListaPuntos[24].DevolverInt() == 50))
                                                        {
                                                            //Devolver Alegria, con 4 de exp
                                                            int[] a = { 1, 4 };
                                                            return a;
                                                        }
                                                        else
                                                        //Else del RightOfLeftEyebrowY
                                                        {
                                                            //MiddleTopOfLeftEyebrowZ   -> Punto 23, ID 49 coordenada X
                                                            if ((ListaPuntos[23].DevolverPunto().Z <= -0.013574) && (ListaPuntos[23].DevolverInt() == 49))
                                                            {
                                                                //Devolver Tristeza, con 116|1 de exp
                                                                int[] a = { 2, 115 };
                                                                return a;
                                                            }
                                                            else
                                                            //Else del MiddleTopOfLeftEyebrowZ
                                                            {
                                                                //Devolver Asco, con 2 de exp
                                                                int[] a = { 5, 2 };
                                                                return a;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            //Else del RightCornerMouthX
                                            {
                                                //MiddleTopOfRightEyebrowZ   -> Punto 6, ID 16 coordenada X                
                                                if ((ListaPuntos[6].DevolverPunto().Z <= -0.026566) && (ListaPuntos[6].DevolverInt() == 16))
                                                {
                                                    //Alegria, con 13 de exp
                                                    int[] a = { 1, 13 };
                                                    return a;
                                                }
                                                else
                                                //Else del MiddleTopOfRightEyebrowZ
                                                {
                                                    //OutsideRightCornerMouthY   -> Punto 16, ID 31 coordenada X    
                                                    if ((ListaPuntos[16].DevolverPunto().Y <= -0.176499) && (ListaPuntos[16].DevolverInt() == 31))
                                                    {
                                                        //Devolver Alegria, con 2|1 de exp
                                                        int[] a = { 1, 1 };
                                                        return a;
                                                    }
                                                    else
                                                    //Else del OutsideRightCornerMouthY
                                                    {
                                                        //Devolver Sorpresa, con 22 de exp
                                                        int[] a = { 3, 22 };
                                                        return a;
                                                    }
                                                }

                                            }
                                        }
                                    }
                                    else
                                    //Else del LeftBottomUpperLipY
                                    {
                                        //Devolver miedo, con 11 de exp
                                        int[] a = { 6, 11 };
                                        return a;
                                    }
                                }
                                else
                                //Else del TopRigthForeheadY
                                {
                                    //MiddleTopOfLeftEyebrowX   -> Punto 23, ID 49 coordenada X  
                                    if ((ListaPuntos[23].DevolverPunto().X <= 0.042786) && (ListaPuntos[23].DevolverInt() == 49))
                                    {
                                        //OutsideRightCornerMouthZ   -> Punto 16, ID 31 coordenada Z  
                                        if ((ListaPuntos[16].DevolverPunto().Z <= 0.010582) && (ListaPuntos[16].DevolverInt() == 31))
                                        {
                                            //ThreeFourthTopRightEyelidY   -> Punto 59, ID 97 coordenada Y  
                                            if ((ListaPuntos[59].DevolverPunto().Y <= -0.084207) && (ListaPuntos[59].DevolverInt() == 97))
                                            {
                                                //RightOfRightEyebrowZ   -> Punto 5, ID 15 coordenada X Z
                                                if ((ListaPuntos[5].DevolverPunto().Z <= -0.020701) && (ListaPuntos[5].DevolverInt() == 15))
                                                {
                                                    //Devolver miedo, con 6 de exp
                                                    int[] a = { 6, 6 };
                                                    return a;
                                                }
                                                else
                                                //Else del RightOfRightEyebrowZ
                                                {
                                                    //RightCornerMouthX   -> Punto 54, ID 89 coordenada X  
                                                    if ((ListaPuntos[54].DevolverPunto().X <= -0.000009) && (ListaPuntos[54].DevolverInt() == 89))
                                                    {
                                                        //AboveThreeFourthRightEyelidX   -> Punto 57, ID 95 coordenada X  
                                                        if ((ListaPuntos[57].DevolverPunto().X <= -0.041247) && (ListaPuntos[57].DevolverInt() == 95))
                                                        {
                                                            //Devolver Alegria, con 23|1 de exp
                                                            int[] a = { 1, 22 };
                                                            return a;
                                                        }
                                                        else
                                                        //Else del AboveThreeFourthRightEyelidX
                                                        {
                                                            //Devolver miedo, con 3 de exp
                                                            int[] a = { 6, 3 };
                                                            return a;
                                                        }
                                                    }
                                                    else
                                                    //Else del RightCornerMouthX
                                                    {
                                                        //Devolver Alegria, con 85 de exp
                                                        int[] a = { 1, 85 };
                                                        return a;
                                                    }
                                                }
                                            }
                                            else
                                            //Else del ThreeFourthTopRightEyelidY
                                            {
                                                //Devolver Tristeza, con 3 de exp
                                                int[] a = { 2, 3 };
                                                return a;
                                            }
                                        }
                                        else
                                        //Else del OutsideRightCornerMouthZ
                                        {
                                            //Devolver miedo, con 15 de exp
                                            int[] a = { 6, 15 };
                                            return a;
                                        }
                                    }
                                    else
                                    //Else del MiddleTopOfLeftEyebrowX
                                    {
                                        //Devolver Tristeza, con 7 de exp
                                        int[] a = { 2, 7 };
                                        return a;
                                    }
                                }
                            }
                        }
                        else
                        //Else del RightSideOfChinX
                        {
                            //BottomOfChinY   -> Punto 4, ID 10 coordenada X                             
                            if ((ListaPuntos[4].DevolverPunto().Y <= -0.221227) && (ListaPuntos[4].DevolverInt() == 10))
                            {
                                //OneFourthBottomRightEyelidY   -> Punto 69, ID 107 coordenada X                             
                                if ((ListaPuntos[69].DevolverPunto().Y <= -0.108275) && (ListaPuntos[69].DevolverInt() == 107))
                                {
                                    //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada X                             
                                    if ((ListaPuntos[21].DevolverPunto().Y <= -0.202922) && (ListaPuntos[21].DevolverInt() == 41))
                                    {
                                        //Devolver miedo, con 8 de exp
                                        int[] a = { 6, 8 };
                                        return a;
                                    }
                                    else
                                    //Else del MiddleBottomLowerLipY
                                    {
                                        //Devolver Alegria, con 12 de exp
                                        int[] a = { 1, 12 };
                                        return a;
                                    }
                                }
                                else
                                //Else del OneFourthBottomRightEyelidY
                                {
                                    //Devolver Tristeza, con 51 de exp
                                    int[] a = { 2, 51 };
                                    return a;
                                }
                            }
                            else
                            //Else del BottomOfChinY
                            {
                                //TopLeftForeheadY   -> Punto 19, ID 34 coordenada X                             
                                if ((ListaPuntos[19].DevolverPunto().Y <= -0.033895) && (ListaPuntos[19].DevolverInt() == 34))
                                {
                                    //OuterCornerOfLeftEyeX   -> Punto 27, ID 53 coordenada X                             
                                    if ((ListaPuntos[27].DevolverPunto().X <= 0.04474) && (ListaPuntos[27].DevolverInt() == 53))
                                    {
                                        //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada X                             
                                        if ((ListaPuntos[21].DevolverPunto().Y <= -0.159597) && (ListaPuntos[21].DevolverInt() == 41))
                                        {
                                            //LeftOfLeftEyebrowX   -> Punto 22, ID 48 coordenada X                             
                                            if ((ListaPuntos[22].DevolverPunto().X <= 0.054434) && (ListaPuntos[22].DevolverInt() == 48))
                                            {
                                                //TopRightForeheadZ   -> Punto 1, ID 1 coordenada Z                             
                                                if ((ListaPuntos[1].DevolverPunto().Z <= -0.028692) && (ListaPuntos[1].DevolverInt() == 1))
                                                {
                                                    //Devolver Enfado, con 2|1 de exp
                                                    int[] a = { 4, 1 };
                                                    return a;
                                                }
                                                else
                                                //Else del TopRightForeheadZ
                                                {
                                                    //Devolver Miedo, con 61|1 de exp
                                                    int[] a = { 6, 60 };
                                                    return a;
                                                }
                                            }
                                            else
                                            //Else del LeftOfLeftEyebrowX
                                            {
                                                //Devolver Asco, con 5 de exp
                                                int[] a = { 5, 5 };
                                                return a;
                                            }
                                        }
                                        else
                                        //Else del MiddleBottomLowerLipY
                                        {
                                            //Devolver Sorpresa, con 7 de exp
                                            int[] a = { 3, 7 };
                                            return a;
                                        }
                                    }
                                    else
                                    //Else del OuterCornerOfLeftEyeX
                                    {
                                        //OutsideLeftCornerMouthY   -> Punto 33, ID 64 coordenada Y                                
                                        if ((ListaPuntos[33].DevolverPunto().Y <= -0.173324) && (ListaPuntos[33].DevolverInt() == 64))
                                        {
                                            //AboveMidUpperRightEyelidY   -> Punto 9, ID 19 coordenada Y                                
                                            if ((ListaPuntos[9].DevolverPunto().Y <= -0.098222) && (ListaPuntos[9].DevolverInt() == 19))
                                            {
                                                //Devolver Sorpresa, con 3 de exp
                                                int[] a = { 3, 3 };
                                                return a;
                                            }
                                            else
                                            //Else del AboveMidUpperRightEyelidY
                                            {
                                                //Devolver Asco, con 3 de exp
                                                int[] a = { 5, 3 };
                                                return a;
                                            }
                                        }
                                        else
                                        //Else del OutsideLeftCornerMouthY
                                        {
                                            //Devolver enfado, con 195 de exp
                                            int[] a = { 4, 195 };
                                            return a;
                                        }
                                    }
                                }
                                else
                                //Else del TopLeftForeheadY
                                {
                                    //RightOfChinY   -> Punto 17, ID 32 coordenada Y                                
                                    if ((ListaPuntos[17].DevolverPunto().Y <= -0.201555) && (ListaPuntos[17].DevolverInt() == 32))
                                    {
                                        //Devolver miedo, con 236 de exp
                                        int[] a = { 6, 236 };
                                        return a;
                                    }
                                    else
                                    //Else del RightOfChinY
                                    {
                                        //TopRightForeheadY   -> Punto 1, ID 1 coordenada Y                                
                                        if ((ListaPuntos[1].DevolverPunto().Y <= -0.034454) && (ListaPuntos[1].DevolverInt() == 1))
                                        {
                                            //Devolver enfado, con 3 de exp
                                            int[] a = { 4, 3 };
                                            return a;
                                        }
                                        else
                                        //Else del TopRightForeheadY
                                        {
                                            //Devolver miedo, con 3|1 de exp
                                            int[] a = { 6, 2 };
                                            return a;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    //Else del OutsiderRightCornerMouthY
                    {
                        //RightOfChinY   -> Punto 17, ID 32 coordenada Y                                                 
                        if ((ListaPuntos[17].DevolverPunto().Y <= -0.172388) && (ListaPuntos[17].DevolverInt() == 32))
                        {
                            //Devolver Alegria, con 144|1 de exp
                            int[] a = { 1, 143 };
                            return a;
                        }
                        else
                        //Else del RightOfChinY
                        {
                            //MiddleTopDipUpperLipZ   -> Punto 2, ID 7 coordenada Z                                                 
                            if ((ListaPuntos[2].DevolverPunto().Z <= 0.001399) && (ListaPuntos[2].DevolverInt() == 7))
                            {
                                //Devolver Tristeza, con 2|1 de exp
                                int[] a = { 2, 1 };
                                return a;
                            }
                            else
                            //Else del MiddleTopDipUpperLipZ
                            {
                                //Devolver Asco, con 2 de exp
                                int[] a = { 5, 2 };
                                return a;
                            }
                        }
                    }
                }
                else
                //Else del LeftSideOfCheekZ
                {
                    //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada Y                                                   
                    if ((ListaPuntos[21].DevolverPunto().Y <= -0.185105) && (ListaPuntos[21].DevolverInt() == 41))
                    {
                        //Devolver Sorpresa, con 43 de exp
                        int[] a = { 3, 43 };
                        return a;
                    }
                    else
                    //Else del MiddleBottomLowerLipY
                    {
                        //InnerCornerLeftEyeY   -> Punto 30, ID 56 coordenada Y                                                   
                        if ((ListaPuntos[30].DevolverPunto().Y <= -0.100238) && (ListaPuntos[30].DevolverInt() == 56))
                        {
                            //InnerCornerLeftEyeY   -> Punto 30, ID 56 coordenada Y                                                   
                            if ((ListaPuntos[30].DevolverPunto().Y <= -0.107078) && (ListaPuntos[30].DevolverInt() == 56))
                            {
                                //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada Y                                                   
                                if ((ListaPuntos[21].DevolverPunto().Y <= -0.182245) && (ListaPuntos[21].DevolverInt() == 41))
                                {
                                    //Devolver Alegria, con 6 de exp
                                    int[] a = { 1, 6 };
                                    return a;
                                }
                                else
                                //Else del MiddleBottomLowerLipY
                                {
                                    //Devolver miedo, con 7 de exp
                                    int[] a = { 6, 7 };
                                    return a;
                                }
                            }
                            else
                            //Else del InnerCornerLeftEyeY
                            {
                                //TopLeftForeheadX   -> Punto 19, ID 37 coordenada X                                                   
                                if ((ListaPuntos[19].DevolverPunto().X <= 0.015384) && (ListaPuntos[19].DevolverInt() == 37))
                                {
                                    //AboveMidUpperRightEyelidY   -> Punto 9, ID 19 coordenada Y                                                  
                                    if ((ListaPuntos[9].DevolverPunto().Y <= -0.09288) && (ListaPuntos[9].DevolverInt() == 11))
                                    {
                                        //Devolver miedo, con 2 de exp
                                        int[] a = { 6, 2 };
                                        return a;
                                    }
                                    else
                                    //Else del AboveMidUpperRightEyelidY
                                    {
                                        //Devolver Asco, con 3 de exp
                                        int[] a = { 5, 3 };
                                        return a;
                                    }
                                }
                                else
                                {
                                    //Devolver asco, con 278 de exp
                                    int[] a = { 5, 278 };
                                    return a;
                                }
                            }
                        }
                        else
                        //Else del InnerCornerLeftEyeY
                        {
                            //LeftTopUpperLipY   -> Punto 45, ID 80 coordenada Y                                                                                     
                            if ((ListaPuntos[45].DevolverPunto().Y <= -0.159686) && (ListaPuntos[45].DevolverInt() == 80))
                            {
                                //Devolver enfado, con 18 de exp
                                int[] a = { 4, 18 };
                                return a;
                            }
                            else
                            //Else del LeftTopUpperLipY
                            {
                                //LeftOfRightEyebrowY   -> Punto 7, ID 17 coordenada Y                                                                                     
                                if ((ListaPuntos[7].DevolverPunto().Y <= -0.082232) && (ListaPuntos[7].DevolverInt() == 17))
                                {
                                    //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada Y                                                                                     
                                    if ((ListaPuntos[21].DevolverPunto().Y <= -0.171045) && (ListaPuntos[21].DevolverInt() == 41))
                                    {
                                        //Devolver asco, con 3 de exp
                                        int[] a = { 5, 3 };
                                        return a;
                                    }
                                    else
                                    //Else del MiddleBottomLowerLipY
                                    {
                                        //Devolver Alegria, con 32|1 de exp
                                        int[] a = { 1, 31 };
                                        return a;
                                    }
                                }
                                else
                                //Else del LeftOfRightEyebrowY
                                {
                                    //InnerCornerLeftEyeY   -> Punto 30, ID 56 coordenada Y                                                                                     
                                    if ((ListaPuntos[30].DevolverPunto().Y <= -0.094747) && (ListaPuntos[30].DevolverInt() == 56))
                                    {
                                        //Devolver Tristeza, con 27 de exp
                                        int[] a = { 2, 27 };
                                        return a;
                                    }
                                    else
                                    //Else del InnerCornerLeftEyeY
                                    {
                                        //OuterCornerOfRightEyeY   -> Punto 10, ID 20 coordenada Y                                                                                     
                                        if ((ListaPuntos[10].DevolverPunto().Y <= -0.094747) && (ListaPuntos[10].DevolverInt() == 20))
                                        {
                                            //Devolver Miedo, con 4 de exp
                                            int[] a = { 6, 4 };
                                            return a;
                                        }
                                        else
                                        //Else del OuterCornerOfRightEyeY
                                        {
                                            //Devolver asco, con 4 de exp
                                            int[] a = { 5, 4 };
                                            return a;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            //Else del LeftOfrightEyebrowY
            {                    
                //BelowThreeFourthLeftEyelidY   -> Punto 64, ID 102 coordenada Y                                                                                     
                if ((ListaPuntos[64].DevolverPunto().Y <= -0.084725) && (ListaPuntos[64].DevolverInt() == 102))
                {                                    
                    //TopLeftForeheadZ   -> Punto 19, ID 34 coordenada Z                                                                                     
                    if ((ListaPuntos[19].DevolverPunto().Z <= 0.027131) && (ListaPuntos[19].DevolverInt() == 34))
                    {
                        //Devolver miedo, con 19|1 de exp
                        int[] a = { 6, 18 };
                        return a;
                    }
                    else
                    //Else del TopLeftForeheadZ
                    {                                         
                        //LeftSideOfCheekX   -> Punto 32, ID 63 coordenada X                                                                                     
                        if ((ListaPuntos[32].DevolverPunto().X <= 0.033601) && (ListaPuntos[32].DevolverInt() == 63))
                        {                                         
                            //MiddleBottomLowerLipY   -> Punto 21, ID 41 coordenada Y                                                                                     
                            if ((ListaPuntos[21].DevolverPunto().Y <= -0.159021) && (ListaPuntos[21].DevolverInt() == 41))
                            {                                 
                                //MiddleTopDipUpperLipY   -> Punto 2, ID 7 coordenada Y                                                                                     
                                if ((ListaPuntos[2].DevolverPunto().Y <= -0.152807) && (ListaPuntos[2].DevolverInt() == 7))
                                {
                                    //Devolver tristeza, con 2 de exp
                                    int[] a = { 2, 2 };
                                    return a;
                                }
                                else
                                //Else del MiddleTopDipUpperLipY
                                {
                                    //Devolver Sorpresa, con 3 de exp
                                    int[] a = { 3, 3 };
                                    return a;
                                }
                            }
                            else
                            //Else del MiddleBottomLowerLipY
                            {
                                //Devolver Sorpresa, con 280 de exp
                                int[] a = { 3, 280 };
                                return a;
                            }
                        }
                        else
                        //Else del LeftSideOfCheekX
                        {
                            //UnderMidBottomLeftEyelidX   -> Punto 31, ID 57 coordenada X                                                                                     
                            if ((ListaPuntos[31].DevolverPunto().X <= 0.029442) && (ListaPuntos[31].DevolverInt() == 57))
                            {
                                //Devolver miedo, con 14 de exp
                                int[] a = { 6, 14 };
                                return a;
                            }
                            else
                            //Else del UnderMidBottomLeftEyelidX
                            {                                      
                                //LeftSideOfCheekX   -> Punto 32, ID 63 coordenada X                                                                                     
                                if ((ListaPuntos[32].DevolverPunto().X <= 0.037464) && (ListaPuntos[32].DevolverInt() == 63))
                                {
                                    //Devolver Sorpresa, con 32 de exp
                                    int[] a = { 3, 32 };
                                    return a;
                                }
                                else
                                //Else del LeftSideOfCheekX
                                {  
                                    //InnerBottomRightPupilX   -> Punto 41, ID 72 coordenada X                                                                                     
                                    if ((ListaPuntos[41].DevolverPunto().X <= -0.022258) && (ListaPuntos[41].DevolverInt() == 72))
                                    {
                                        //Devolver miedo, con 3 de exp
                                        int[] a = { 6, 3 };
                                        return a;
                                    }
                                    else
                                    //Else del InnerBottomRightPupilX
                                    {
                                        //Devolver Sorpresa, con 2 de exp
                                        int[] a = { 3, 2 };
                                        return a;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                //Else del BelowThreeFourthLeftEyelidY
                {
                    //Devolver Asco, con 11 de exp
                    int[] a = { 5, 11 };
                    return a;
                }
            }
        }


        public int[] ComprobarEmocion_2Arbol_Nariz_17Personas(List<Point3D> PuntosCara)
        {
            //Aqui es donde se implementarán los if-then-else del arbol de decision
            //Si no hay puntos terminamos la ejecución
            if (PuntosCara.Count == 0)
            {
                int[] a = { 10, 0 };
                return a;
            }

            //Realizamos el traslado de los puntos, con TopSkull
            this.TraslacionNariz(PuntosCara);

            //OuterTopLeftPupilX -> Punto 38, ID 69 coordenada X
            if ((ListaPuntos[38].DevolverPunto().X <= -0.033112) && (ListaPuntos[38].DevolverInt() == 69))
            {
                //MiddleBottomUpperLipZ -> Punto 52, ID 87 coordenada Z
                if((ListaPuntos[52].DevolverPunto().Z <= 0.012095) && (ListaPuntos[52].DevolverInt() == 87))
                {
                    //MiddleTopLowerLipX -> Punto 20, ID 40 coordenada X
                    if((ListaPuntos[20].DevolverPunto().X <= 0.00602) && (ListaPuntos[20].DevolverInt() == 40))
                    {                        
                        //Devolver Sorpresa, con 11|1 de exp
                        int[] a = { 3, 10 };
                        return a;
                    }
                    //Else del MiddleTopLowerLipX
                    else
                    {
                        //OuterTopLeftPupilY -> Punto 38, ID 69 coordenada X
                        if((ListaPuntos[38].DevolverPunto().Y <= 0.012393) && (ListaPuntos[38].DevolverInt() == 69))
                        {
                            //TopSkullZ -> Punto 0, ID 0 coordenada Z
                            if((ListaPuntos[0].DevolverPunto().Z <= 0.030302) && (ListaPuntos[0].DevolverInt() == 0))
                            {
                                //MiddleBottomLeftEyelidX -> Punto 29, ID 55 coordenada X
                                if((ListaPuntos[29].DevolverPunto().X <= 0.030611) && (ListaPuntos[29].DevolverInt() == 55))
                                {
                                    //Devolver Alegria, con 2 de exp
                                    int[] a = { 1, 2 };
                                    return a;
                                }
                                //Else del MiddleBottomLeftEyelidX
                                else
                                {
                                    //Devolver Tristeza, con 8 de exp
                                    int[] a = { 2, 8 };
                                    return a;
                                }
                            }
                            //Else del TopSkullZ
                            else
                            {
                                //MiddleBottomOfLeftEyeBrowX -> Punto 25, ID 51 coordenada X
                                if((ListaPuntos[25].DevolverPunto().X <= 0.015125) && (ListaPuntos[25].DevolverInt() == 51))
                                {
                                    //UnderMidBottomLeftEyelidZ -> Punto 31, ID 57 coordenada Z
                                    if((ListaPuntos[31].DevolverPunto().Z <= 0.009922) && (ListaPuntos[31].DevolverInt() == 57))
                                    {
                                        //MiddleTopLowerLipZ -> Punto 20, ID 40 coordenada Z
                                        if((ListaPuntos[20].DevolverPunto().Z <= 0.002997) && (ListaPuntos[20].DevolverInt() == 40))
                                        {
                                            //Devolver Neutra, con 2 de exp
                                            int[] a = { 7, 2 };
                                            return a;
                                        }
                                        //Else del MiddleTopLowerLipZ
                                        else
                                        {
                                            //Devolver Tristeza, con 4|1 de exp
                                            int[] a = { 2, 3 };
                                            return a;
                                        }
                                    }
                                    //Else del UnderMidBottomLeftEyelidZ
                                    else
                                    {
                                        //RightTopDipUpperLipZ -> Punto 18, ID 33 coordenada Z
                                        if((ListaPuntos[18].DevolverPunto().Z <= 0.01966) && (ListaPuntos[18].DevolverInt() == 33))
                                        {
                                            //Devolver Neutra, con 306|3 de exp
                                            int[] a = { 7, 303 };
                                            return a;
                                        }
                                        //Else del RightTopDipUpperLipZ
                                        else
                                        {
                                            //RightTopUpperLipY -> Punto 44, ID 79 coordenada Y
                                            if((ListaPuntos[44].DevolverPunto().Y <= 0.008219) && (ListaPuntos[44].DevolverInt() == 79))
                                            {
                                                //Devolver Enfado, con 8 de exp
                                                int[] a = { 4, 8 };
                                                return a;
                                            }
                                            //Else del RightTopUpperLipY
                                            else
                                            {
                                                //Devolver Neutra, con 76 de exp
                                                int[] a = { 7, 76 };
                                                return a;
                                            }
                                        }
                                    }
                                }
                                //Else del MiddleBottomOfLeftEyeBrowX
                                else
                                {
                                    //UnderMidBottomRightEyelidX -> Punto 14, ID 24 coordenada X
                                    if((ListaPuntos[14].DevolverPunto().X <= -0.015807) && (ListaPuntos[14].DevolverInt() == 24))
                                    {
                                        //MiddleBottomRightEyelidY -> Punto 12, ID 22 coordenada Y
                                        if((ListaPuntos[12].DevolverPunto().Y <= -0.033112) && (ListaPuntos[12].DevolverInt() == 22))
                                        {
                                            //Devolver Tristeza, con 3 de exp
                                            int[] a = { 2, 3 };
                                            return a;
                                        }
                                        //Else del MiddleBottomRightEyelidY
                                        else
                                        {
                                            //Devolver Neutra, con 2 de exp
                                            int[] a = { 7, 2 };
                                            return a;
                                        }
                                    }
                                    //Else del UnderMidBottomRightEyelidX
                                    else
                                    {
                                        //Devolver Alegria, con 4 de exp
                                        int[] a = { 1, 4 };
                                        return a;
                                    }
                                }
                            }
                        }
                        //Else del OuterTopLeftPupilY
                        else
                        {
                            //Devolver Alegria, con 9 de exp
                            int[] a = { 1, 9 };
                            return a;
                        }
                    }
                }
                //Else del MiddleBottomUpperLipZ
                else
                {
                    //OneFourthTopRightEyelidX -> Punto 67, ID 105 coordenada X
                    if((ListaPuntos[67].DevolverPunto().X <= 0.025091) && (ListaPuntos[67].DevolverInt() == 105))
                    {
                        //MiddleBottomLowerLipZ -> Punto 21, ID 41 coordenada Z
                        if((ListaPuntos[21].DevolverPunto().Z <= 0.02273) && (ListaPuntos[21].DevolverInt() == 41))
                        {
                            //UnderMidBottomRightEyelidX -> Punto 14, ID 24 coordenada X
                            if((ListaPuntos[14].DevolverPunto().X <= -0.017888) && (ListaPuntos[14].DevolverInt() == 24))
                            {
                                //OneFourthTopRighEyelidX -> Punto 67, ID 105 coordenada X
                                if((ListaPuntos[67].DevolverPunto().X <= 0.019534) && (ListaPuntos[67].DevolverInt() == 105))
                                {
                                    //Devolver Sorpresa, con 18|1 de exp
                                    int[] a = { 3, 18 };
                                    return a;
                                }
                                //Else del OneFourthTopRighEyelidX
                                else
                                {
                                    //RightSideOfChinX -> Punto 15, ID 30 coordenada X
                                    if((ListaPuntos[15].DevolverPunto().X <= -0.037464) && (ListaPuntos[15].DevolverInt() == 30))
                                    {
                                        //Devolver Alegria, con 9 de exp
                                        int[] a = { 1, 9 };
                                        return a;
                                    }
                                    //Else del RightSideOfChinX
                                    else
                                    {
                                        //Devolver Asco, con 100 de exp
                                        int[] a = { 5, 100 };
                                        return a;
                                    }
                                }
                            }
                            //Else del UnderMidBottomRightEyelidX
                            else
                            {
                                //BottomOfLeftCheekX -> Punto 56, ID 91 coordenada X
                                if((ListaPuntos[56].DevolverPunto().X <= -0.049516) && (ListaPuntos[56].DevolverInt() == 91))
                                {
                                    //LeftBottomUpperLipY -> Punto 47, ID 82 coordenada Y
                                    if((ListaPuntos[47].DevolverPunto().Y <= -0.054898) && (ListaPuntos[47].DevolverInt() == 82))
                                    {
                                        //Devolver Neutra, con 26 de exp
                                        int[] a = { 7, 26 };
                                        return a;
                                    }
                                    //Else del LeftBottomUpperLipY
                                    else
                                    {
                                        //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                        if((ListaPuntos[31].DevolverPunto().Y <= 0.012789) && (ListaPuntos[31].DevolverInt() == 57))
                                        {
                                            //MiddleTopOfRightEyebrowY -> Punto 6, ID 16 coordenada Y
                                            if((ListaPuntos[6].DevolverPunto().Y <= 0.026124) && (ListaPuntos[6].DevolverInt() == 16))
                                            {
                                                //UnderMidBottomRightEyelidX -> Punto 14, ID 24 coordenada X
                                                if((ListaPuntos[14].DevolverPunto().X <= -0.016272) && (ListaPuntos[14].DevolverInt() == 24))
                                                {
                                                    //Devolver Alegria, con 5 de exp
                                                    int[] a = { 1, 5 };
                                                    return a;
                                                }
                                                //Else del UnderMidBottomRightEyelidX
                                                else
                                                {
                                                    //Devolver Enfado, con 35|1 de exp
                                                    int[] a = { 4, 34 };
                                                    return a;
                                                }
                                            }
                                            //Else del MiddleTopOfRightEyebrowY
                                            else
                                            {
                                                //MiddleBottomOfLeftEyebrowX -> Punto 25, ID 51 coordenada X
                                                if((ListaPuntos[25].DevolverPunto().X <= 0.022542) && (ListaPuntos[25].DevolverInt() == 51))
                                                {
                                                    //BottomOfRightCheekZ -> Punto 55, ID 90 coordenada Z
                                                    if((ListaPuntos[55].DevolverPunto().Z <= 0.025755) && (ListaPuntos[55].DevolverInt() == 90))
                                                    {
                                                        //Devolver Tristeza, con 5|1 de exp
                                                        int[] a = { 2, 4 };
                                                        return a;
                                                    }
                                                    //Else del BottomOfRightCheekZ
                                                    else
                                                    {
                                                        //Devolver Alegria, con 290|7 de exp
                                                        int[] a = { 1, 290 };
                                                        return a;
                                                    }
                                                }
                                                //Else del MiddleBottomOfLeftEyebrowX
                                                else
                                                {
                                                    //Devolver Enfado, con 5 de exp
                                                    int[] a = { 4, 5 };
                                                    return a;
                                                }
                                            }
                                        }
                                        //Else del UnderMidBottomLeftEyelidY
                                        else
                                        {
                                            //Devolver Sorpresa, con 10 de exp
                                            int[] a = { 3, 10 };
                                            return a;
                                        }
                                    }
                                }
                                //Else del BottomOfLeftCheekX
                                else
                                {
                                    //UnderMidBottomLeftEyelidX -> Punto 31, ID 57 coordenada X
                                    if((ListaPuntos[31].DevolverPunto().X <= 0.013416) && (ListaPuntos[31].DevolverInt() == 57))
                                    {
                                        //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada X
                                        if((ListaPuntos[31].DevolverPunto().Y <= 0.007931) && (ListaPuntos[31].DevolverInt() == 57))
                                        {
                                            //InnerCornerRightEyeX -> Punto 13, ID 23 coordenada X
                                            if((ListaPuntos[13].DevolverPunto().X <= -0.030405) && (ListaPuntos[13].DevolverInt() == 23))
                                            {
                                                //Devolver Tristeza, con 5 de exp
                                                int[] a = { 2, 5 };
                                                return a;
                                            }
                                            //Else del InnerCornerRightEyeX
                                            else
                                            {
                                                //Devolver Miedo, con 2 de exp
                                                int[] a = { 6, 2 };
                                                return a;
                                            }
                                        }
                                        //Else del UnderMidBottomLeftEyelidY
                                        else
                                        {
                                            //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                            if((ListaPuntos[31].DevolverPunto().Y <= 0.014346) && (ListaPuntos[31].DevolverInt() == 57))
                                            {
                                                //AboveMidUpperRightEyelidZ -> Punto 9, ID 19 coordenada Z
                                                if((ListaPuntos[9].DevolverPunto().Z <= -0.000325) && (ListaPuntos[9].DevolverInt() == 19))
                                                {
                                                    //RightTopLowerLipY -> Punto 48, ID 83 coordenada Y
                                                    if((ListaPuntos[48].DevolverPunto().Y <= -0.056159) && (ListaPuntos[48].DevolverInt() == 83))
                                                    {
                                                        //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                        if((ListaPuntos[0].DevolverPunto().Y <= 0.114745) && (ListaPuntos[0].DevolverInt() == 0))
                                                        {
                                                            //Devolver Sorpresa, con 3 de exp
                                                            int[] a = { 3, 3 };
                                                            return a;
                                                        }
                                                        //Else del TopSkullY
                                                        else
                                                        {
                                                            //Devolver Enfado, con 2 de exp
                                                            int[] a = { 4, 2 };
                                                            return a;
                                                        }
                                                    }
                                                    //Else del RightTopLowerLipY
                                                    else
                                                    {
                                                        //OutsideLeftCornerMouthY -> Punto 33, ID 64 coordenada Y
                                                        if((ListaPuntos[33].DevolverPunto().Y <= -0.06299) && (ListaPuntos[33].DevolverInt() == 64))
                                                        {
                                                            //Devolver Tristeza, con 5|1 de exp
                                                            int[] a = { 2, 4 };
                                                            return a;
                                                        }
                                                        //Else del OutsideLeftCornerMouthY
                                                        else
                                                        {
                                                            //UnderMidBottomRightEyelidZ -> Punto 14, ID 24 coordenada Z
                                                            if((ListaPuntos[14].DevolverPunto().Z <= 0.002537) && (ListaPuntos[14].DevolverInt() == 24))
                                                            {
                                                                //Devolver Tristeza, con 4|1 de exp
                                                                int[] a = { 2, 3 };
                                                                return a;
                                                            }
                                                            //Else del OuterTopLeftPupilX
                                                            else
                                                            {
                                                                //Devolver Alegria, con 261|4 de exp
                                                                int[] a = { 1, 257 };
                                                                return a;
                                                            }
                                                        }
                                                    }
                                                }
                                                //Else del AboveMidUpperRightEyelidZ
                                                else
                                                {
                                                    //Devolver Enfado, con 8|1 de exp
                                                    int[] a = { 4, 7 };
                                                    return a;
                                                }
                                            }
                                            //Else del UnderMidBottomLeftEyelidY
                                            else
                                            {
                                                //Devolver Sorpresa, con 7 de exp
                                                int[] a = { 3, 7 };
                                                return a;
                                            }
                                        }
                                    }
                                    //Else del UnderMidBottomLeftEyelidX
                                    else
                                    {
                                        //MiddleBottomOfRightEyebrowY -> Punto 8, ID 18 coordenada Y
                                        if((ListaPuntos[8].DevolverPunto().Y <= 0.024673) && (ListaPuntos[8].DevolverInt() == 18))
                                        {
                                            //RightCornerMouthY -> Punto 54, ID 89 coordenada Y
                                            if((ListaPuntos[54].DevolverPunto().Y <= -0.055111) && (ListaPuntos[54].DevolverInt() == 89))
                                            {
                                                //MiddleTopLowerLipY -> Punto 20, ID 40 coordenada Y
                                                if((ListaPuntos[20].DevolverPunto().Y <= 0.079143) && (ListaPuntos[20].DevolverInt() == 40))
                                                {
                                                    //RightTopDipUpperLipY -> Punto 18, ID 33 coordenada Y
                                                    if((ListaPuntos[18].DevolverPunto().Y <= -0.085413) && (ListaPuntos[18].DevolverInt() == 33))
                                                    {
                                                        //MiddleBottomOfRightEyebrowZ -> Punto 8, ID 18 coordenada Z
                                                        if((ListaPuntos[8].DevolverPunto().Z <= -0.003023) && (ListaPuntos[8].DevolverInt() == 18))
                                                        {
                                                            //Devolver Tristeza, con 27 de exp
                                                            int[] a = { 2, 27 };
                                                            return a;
                                                        }
                                                        //Else del OuterTopLeftPupilX
                                                        else
                                                        {
                                                            //Devolver Alegria, con 2 de exp
                                                            int[] a = { 1, 2 };
                                                            return a;
                                                        }
                                                    }
                                                    //Else del RightTopDipUpperLipY
                                                    else
                                                    {
                                                        //Devolver Miedo, con 3|2 de exp
                                                        int[] a = { 6, 1 };
                                                        return a;
                                                    }
                                                }
                                                //Else del MiddleTopLowerLipY
                                                else
                                                {
                                                    //UnderMidBottomRightEyelidX -> Punto 14, ID 24 coordenada X
                                                    if((ListaPuntos[14].DevolverPunto().X <= -0.016325) && (ListaPuntos[14].DevolverInt() == 24))
                                                    {
                                                        //AboveChinZ -> Punto 3, ID 9 coordenada Z
                                                        if((ListaPuntos[3].DevolverPunto().Z <= 0.002574) && (ListaPuntos[3].DevolverInt() == 9))
                                                        {
                                                            //Devolver Tristeza, con 4|1 de exp
                                                            int[] a = { 2, 3 };
                                                            return a;
                                                        }
                                                        //Else del AboveChinZ
                                                        else
                                                        {
                                                            //RightCornerMouthZ -> Punto 54, ID 89 coordenada Z
                                                            if((ListaPuntos[54].DevolverPunto().Z <= 0.016233) && (ListaPuntos[54].DevolverInt() == 89))
                                                            {
                                                                //Devolver Enfado, con 3 de exp
                                                                int[] a = { 4, 3 };
                                                                return a;
                                                            }
                                                            //Else del RightCornerMouthZ
                                                            else
                                                            {
                                                                //Devolver Alegria, con 31|2 de exp
                                                                int[] a = { 1, 29 };
                                                                return a;
                                                            }
                                                        }
                                                    }
                                                    //Else del UnderMidBottomRightEyelidX
                                                    else
                                                    {
                                                        //RightBottomLowerLipZ -> Punto 50, ID 85 coordenada Z
                                                        if((ListaPuntos[50].DevolverPunto().Z <= 0.021773) && (ListaPuntos[50].DevolverInt() == 85))
                                                        {
                                                            //Devolver Enfado, con 112|1 de exp
                                                            int[] a = { 4, 111 };
                                                            return a;
                                                        }
                                                        //Else del RightBottomLowerLipZ
                                                        else
                                                        {
                                                            //Devolver Alegria, con 2 de exp
                                                            int[] a = { 1, 2 };
                                                            return a;
                                                        }
                                                    }
                                                }
                                            }
                                            //Else del RightCornerMouthY
                                            else
                                            {
                                                //RightOfChinZ -> Punto 17, ID 32 coordenada Z
                                                if((ListaPuntos[17].DevolverPunto().Z <= 0.030956) && (ListaPuntos[17].DevolverInt() == 32))
                                                {
                                                    //MiddleTopDipUpperLipY -> Punto 2, ID 7 coordenada Y
                                                    if((ListaPuntos[2].DevolverPunto().Y <= 0.07173) && (ListaPuntos[2].DevolverInt() == 7))
                                                    {
                                                        //AboveOneFourthLeftEyelidZ -> Punto 66, ID 104 coordenada Z
                                                        if((ListaPuntos[66].DevolverPunto().Z <= 0.007367) && (ListaPuntos[66].DevolverInt() == 104))
                                                        {
                                                            //OutsideLeftCornerMouthZ -> Punto 33, ID 64 coordenada Z
                                                            if((ListaPuntos[33].DevolverPunto().Z <= 0.059602) && (ListaPuntos[33].DevolverInt() == 64))
                                                            {
                                                                //LeftOfLeftEyebrowY -> Punto 22, ID 48 coordenada Y
                                                                if((ListaPuntos[22].DevolverPunto().Y <= -0.055565) && (ListaPuntos[22].DevolverInt() == 48))
                                                                {
                                                                    //Devuelve Tristeza, con 37|1 de exp
                                                                    int[] a = { 2, 36 };
                                                                    return a;
                                                                }
                                                                //Else del LeftOfLeftEyebrowY
                                                                else
                                                                {
                                                                    //Devuelve Miedo, con 28 de exp
                                                                    int[] a = { 6, 28 };
                                                                    return a;
                                                                }
                                                            }
                                                            //Else del OutsideLeftCornerMouthZ
                                                            else
                                                            {
                                                                //UnderMidBottomRightEyelidY -> Punto 14, ID 24 coordenada Y
                                                                if((ListaPuntos[14].DevolverPunto().Y <= 0.011853) && (ListaPuntos[14].DevolverInt() == 24))
                                                                {
                                                                    //Devolver Tristeza, con 861|9 de exp
                                                                    int[] a = { 2, 852 };
                                                                    return a;
                                                                }
                                                                //Else del UnderMidBottomRightEyelidY
                                                                else
                                                                {
                                                                    //LeftSideOfCheekX -> Punto 32, ID 63 coordenada X
                                                                    if((ListaPuntos[32].DevolverPunto().X <= 0.028541) && (ListaPuntos[32].DevolverInt() == 63))
                                                                    {
                                                                        //Devolver Miedo, con 8 de exp
                                                                        int[] a = { 6, 8 };
                                                                        return a;
                                                                    }
                                                                    //Else del LeftSideOfCheekX
                                                                    else
                                                                    {
                                                                        //MiddleTopRightEyelidY -> Punto 11, ID 21 coordenada Y
                                                                        if((ListaPuntos[11].DevolverPunto().Y <= 0.017296) && (ListaPuntos[11].DevolverInt() == 21))
                                                                        {
                                                                            //Devolver Alegria, con 3 de exp
                                                                            int[] a = { 1, 3 };
                                                                            return a;
                                                                        }
                                                                        //Else del OuterTopLeftPupilX
                                                                        else
                                                                        {
                                                                            //Devolver Tristeza, con 3 de exp
                                                                            int[] a = { 2, 3 };
                                                                            return a;
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        //Else del AboveOneFourthLeftEyelidZ
                                                        else
                                                        {
                                                            //LeftOfLeftEyebrowY -> Punto 22, ID 48 coordenada Y
                                                            if((ListaPuntos[22].DevolverPunto().Y <= -0.058871) && (ListaPuntos[22].DevolverInt() == 48))
                                                            {
                                                                //TopSkullX -> Punto 0, ID 0 coordenada X
                                                                if((ListaPuntos[0].DevolverPunto().X <= -0.012183) && (ListaPuntos[0].DevolverInt() == 0))
                                                                {
                                                                    //Devolver Tristeza, con 2 de exp
                                                                    int[] a = { 2, 2 };
                                                                    return a;
                                                                }
                                                                //Else del TopSkullX
                                                                else
                                                                {
                                                                    //Devolver Sorpresa, con 2 de exp
                                                                    int[] a = { 3, 2 };
                                                                    return a;
                                                                }
                                                            }
                                                            //Else del LeftOfLeftEyebrowY
                                                            else
                                                            {
                                                                //Devolver Miedo, con 10 de exp
                                                                int[] a = { 6, 10 };
                                                                return a;
                                                            }
                                                        }
                                                    }
                                                    //Else del MiddleTopDipUpperLipY
                                                    else
                                                    {
                                                        //LeftBottomUpperLipY -> Punto 47, ID 82 coordenada Y
                                                        if((ListaPuntos[47].DevolverPunto().Y <= -0.04846) && (ListaPuntos[47].DevolverInt() == 82))
                                                        {
                                                            //RightOfLeftEyebrowY -> Punto 24, ID 50 coordenada Y
                                                            if((ListaPuntos[24].DevolverPunto().Y <= 0.034942) && (ListaPuntos[24].DevolverInt() == 50))
                                                            {
                                                                //MiddleBottomLowerLipY -> Punto 21, ID 41 coordenada Y
                                                                if((ListaPuntos[21].DevolverPunto().Y <= -0.044636) && (ListaPuntos[21].DevolverInt() == 41))
                                                                {
                                                                    //MiddleTopDipUpperLipY -> Punto 2, ID 7 coordenada Y
                                                                    if((ListaPuntos[2].DevolverPunto().Y <= 0.088857) && (ListaPuntos[2].DevolverInt() == 7))
                                                                    {
                                                                        //RightOfChinX -> Punto 17, ID 32 coordenada X
                                                                        if((ListaPuntos[17].DevolverPunto().X <= -0.031225) && (ListaPuntos[17].DevolverInt() == 32))
                                                                        {
                                                                            //BottomOfChinZ -> Punto 4, ID 10 coordenada Z
                                                                            if((ListaPuntos[4].DevolverPunto().Z <= 0.013651) && (ListaPuntos[4].DevolverInt() == 10))
                                                                            {
                                                                                //RightOfRightEyebrowZ -> Punto 5, ID 15 coordenada Z
                                                                                if((ListaPuntos[5].DevolverPunto().Z <= 0.021859) && (ListaPuntos[5].DevolverInt() == 15))
                                                                                {
                                                                                    //Devolver Tristeza, con 17 de exp
                                                                                    int[] a = { 2, 17 };
                                                                                    return a;
                                                                                }
                                                                                //Else del OuterTopLeftPupilX
                                                                                else
                                                                                {
                                                                                    //Devolver Enfado, con 8 de exp
                                                                                    int[] a = { 4, 8 };
                                                                                    return a;
                                                                                }
                                                                            }
                                                                            //Else del BottomOfChinZ
                                                                            else
                                                                            {
                                                                                //OneFourthTopRightEyelidY -> Punto 67, ID 105 coordenada Y
                                                                                if((ListaPuntos[67].DevolverPunto().Y <= 0.018308) && (ListaPuntos[67].DevolverInt() == 105))
                                                                                {
                                                                                    //Devolver Alegria, con 27|1 de exp
                                                                                    int[] a = { 1, 26 };
                                                                                    return a;
                                                                                }
                                                                                //Else del OneFourthTopRightEyelidY
                                                                                else
                                                                                {
                                                                                    //Devolver Sorpresa, con 4|1 de exp
                                                                                    int[] a = { 3, 3 };
                                                                                    return a;
                                                                                }
                                                                            }
                                                                        }
                                                                        //Else del RightOfChinX
                                                                        else
                                                                        {
                                                                            //MiddleBottomOfLeftEyebrowZ -> Punto 25, ID 51 coordenada Z
                                                                            if((ListaPuntos[25].DevolverPunto().Z <= -0.006113) && (ListaPuntos[25].DevolverInt() == 51))
                                                                            {
                                                                                //OuterTopRightPupilX -> Punto 36, ID 67 coordenada X
                                                                                if((ListaPuntos[36].DevolverPunto().X <= 0.003844) && (ListaPuntos[36].DevolverInt() == 67))
                                                                                {
                                                                                    //InnerCornerRightEyeX -> Punto 13, ID 23 coordenada X
                                                                                    if((ListaPuntos[13].DevolverPunto().X <= -0.03085) && (ListaPuntos[13].DevolverInt() == 23))
                                                                                    {
                                                                                        //Devolver Asco, con 29|1 de exp
                                                                                        int[] a = { 5, 28 };
                                                                                        return a;
                                                                                    }
                                                                                    //Else del InnerCornerRightEyeX
                                                                                    else
                                                                                    {
                                                                                        //Devolver Sorpresa, con 5|2 de exp
                                                                                        int[] a = { 3, 3 };
                                                                                        return a;
                                                                                    }
                                                                                }
                                                                                //Else del OuterTopRightPupilX
                                                                                else
                                                                                {
                                                                                    //RightOfChinY -> Punto 17, ID 32 coordenada Y
                                                                                    if((ListaPuntos[17].DevolverPunto().Y <= -0.049868) && (ListaPuntos[17].DevolverInt() == 32))
                                                                                    {
                                                                                        //MiddleBottomOfRightEyebrowX -> Punto 8, ID 18 coordenada X
                                                                                        if((ListaPuntos[8].DevolverPunto().X <= -0.013258) && (ListaPuntos[8].DevolverInt() == 18))
                                                                                        {
                                                                                            //UnderMidBottomLeftEyelidX -> Punto 31, ID 57 coordenada X
                                                                                            if((ListaPuntos[31].DevolverPunto().X <= 0.016154) && (ListaPuntos[31].DevolverInt() == 57))
                                                                                            {
                                                                                                //Devuelve Tristeza, con 488|7
                                                                                                int[] a = { 2, 481 };
                                                                                                return a;
                                                                                            }
                                                                                            //Else del UnderMidBottomLeftEyelidX
                                                                                            else
                                                                                            {
                                                                                                //UnderMidBottomRightEyelidX -> Punto 12, ID 22 coordenada X
                                                                                                if((ListaPuntos[12].DevolverPunto().X <= -0.015109) && (ListaPuntos[12].DevolverInt() == 22))
                                                                                                {
                                                                                                    //Devolver Asco, con 8 de exp
                                                                                                    int[] a = { 5, 8 };
                                                                                                    return a;
                                                                                                }
                                                                                                //Else del UnderMidBottomRightEyelidX
                                                                                                else
                                                                                                {
                                                                                                    //Devolver Tristeza, con 11 de exp
                                                                                                    int[] a = { 2, 11 };
                                                                                                    return a;
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                        //Else del MiddleBottomOfRightEyebrowX
                                                                                        else
                                                                                        {
                                                                                            //RightBottomUpperLipZ -> Punto 46, ID 81 coordenada Z
                                                                                            if((ListaPuntos[46].DevolverPunto().Z <= 0.01993) && (ListaPuntos[46].DevolverInt() == 81))
                                                                                            {
                                                                                                //Devolver Asco, con 15|1 de exp
                                                                                                int[] a = { 5, 14 };
                                                                                                return a;
                                                                                            }
                                                                                            //Else del RightBottomUpperLipZ
                                                                                            else
                                                                                            {
                                                                                                //UnderMidBottomLeftEyelidX -> Punto 31, ID 57 coordenada X
                                                                                                if((ListaPuntos[31].DevolverPunto().X <= 0.014798) && (ListaPuntos[31].DevolverInt() == 57))
                                                                                                {
                                                                                                    //Devolver Tristeza, con 8 de exp
                                                                                                    int[] a = { 2, 8 };
                                                                                                    return a;
                                                                                                }
                                                                                                //Else del OuterTopLeftPupilX
                                                                                                else
                                                                                                {
                                                                                                    //Devolver Sorpresa, con 2 de exp
                                                                                                    int[] a = { 3, 2 };
                                                                                                    return a;
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                    //Else del RightOfChinY
                                                                                    else
                                                                                    {
                                                                                        //Devolver Asco, con 19|1 de exp
                                                                                        int[] a = { 5, 18 };
                                                                                        return a;
                                                                                    }
                                                                                }
                                                                            }
                                                                            //Else del MiddleBottomOfLeftEyebrowZ
                                                                            else
                                                                            {
                                                                                //RightTopDipUpperLipZ -> Punto 18, ID 33 coordenada Z
                                                                                if((ListaPuntos[18].DevolverPunto().Z <= 0.030532) && (ListaPuntos[18].DevolverInt() == 33))
                                                                                {
                                                                                    //RightTopLowerLipZ -> Punto 48, ID 83 coordenada Z
                                                                                    if((ListaPuntos[48].DevolverPunto().Z <= 0.018015) && (ListaPuntos[48].DevolverInt() == 83))
                                                                                    {
                                                                                        //OutsideRightCornerMouthZ -> Punto 16, ID 31 coordenada Z
                                                                                        if((ListaPuntos[16].DevolverPunto().Z <= 0.058342) && (ListaPuntos[16].DevolverInt() == 31))
                                                                                        {
                                                                                            //OneFourthBottomLeftEyelidY -> Punto 70, ID 108 coordenada Y
                                                                                            if((ListaPuntos[70].DevolverPunto().Y <= 0.008702) && (ListaPuntos[70].DevolverInt() == 108))
                                                                                            {
                                                                                                //Devolver Enfado, con 9|1 de exp
                                                                                                int[] a = { 4, 8 };
                                                                                                return a;
                                                                                            }
                                                                                            //Else del OneFourthBottomLeftEyelidY
                                                                                            else
                                                                                            {
                                                                                                //Devolver Tristeza, con 212|4 de exp
                                                                                                int[] a = { 2, 208 };
                                                                                                return a;
                                                                                            }
                                                                                        }
                                                                                        //Else del OutsideRightCornerMouthZ
                                                                                        else
                                                                                        {
                                                                                            //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                                                                            if((ListaPuntos[31].DevolverPunto().Y <= -0.033112) && (ListaPuntos[31].DevolverInt() == 57))
                                                                                            {
                                                                                                //Devolver Alegria, con 2 de exp
                                                                                                int[] a = { 1, 2 };
                                                                                                return a;
                                                                                            }
                                                                                            //Else del UnderMidBottomLeftEyelidY
                                                                                            else
                                                                                            {
                                                                                                //Devolver Enfado, con 10 de exp
                                                                                                int[] a = { 4, 10 };
                                                                                                return a;
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                    //Else del RightTopLowerLipZ
                                                                                    else
                                                                                    {
                                                                                        //Devolver Enfado, con 7|1 de exp
                                                                                        int[] a = { 4, 6 };
                                                                                        return a;
                                                                                    }
                                                                                }
                                                                                //Else del RightTopDipUpperLipZ
                                                                                else
                                                                                {
                                                                                    //UnderMidBottomRightEyelidY -> Punto 14, ID 24 coordenada Y
                                                                                    if((ListaPuntos[14].DevolverPunto().Y <= 0.011812) && (ListaPuntos[14].DevolverInt() == 24))
                                                                                    {
                                                                                        //Devolver Enfado, con 18|1 de exp
                                                                                        int[] a = { 4, 17 };
                                                                                        return a;
                                                                                    }
                                                                                    //Else del UnderMidBottomRightEyelidY
                                                                                    else
                                                                                    {
                                                                                        //ThreeFourthBottomLeftEyelidX -> Punto 62, ID 100 coordenada X
                                                                                        if((ListaPuntos[62].DevolverPunto().X <= -0.043141) && (ListaPuntos[62].DevolverInt() == 100))
                                                                                        {
                                                                                            //Devolver Alegria, con 35 de exp
                                                                                            int[] a = { 1, 35 };
                                                                                            return a;
                                                                                        }
                                                                                        //Else del OuterTopLeftPupilX
                                                                                        else
                                                                                        {
                                                                                            //Devolver Sorpresa, con 4 de exp
                                                                                            int[] a = { 3, 4 };
                                                                                            return a;
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                    //Else del MiddleTopDipUpperLipY
                                                                    else
                                                                    {
                                                                        //MiddleBottomOfRightEyebrowY -> Punto 8, ID 18 coordenada Y
                                                                        if((ListaPuntos[8].DevolverPunto().Y <= 0.020064) && (ListaPuntos[8].DevolverInt() == 18))
                                                                        {
                                                                            //Devolver Alegria, con 36 de exp
                                                                            int[] a = { 1, 36 };
                                                                            return a;
                                                                        }
                                                                        //Else del MiddleBottomOfRightEyebrowY
                                                                        else
                                                                        {
                                                                            //Devolver Tristeza, con 3 de exp
                                                                            int[] a = { 2, 3 };
                                                                            return a;
                                                                        }
                                                                    }
                                                                }
                                                                //Else del MiddleBottomLowerLipY
                                                                else
                                                                {
                                                                    //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                                    if((ListaPuntos[0].DevolverPunto().Y <= 0.132744) && (ListaPuntos[0].DevolverInt() == 0))
                                                                    {
                                                                        //Devolver Tristeza, con 2 de exp
                                                                        int[] a = { 2, 2 };
                                                                        return a;
                                                                    }
                                                                    //Else del TopSkullY
                                                                    else
                                                                    {
                                                                        //Devolver Asco, con 34 de exp
                                                                        int[] a = { 5, 34 };
                                                                        return a;
                                                                    }
                                                                }
                                                            }
                                                            //Else del RightOfLeftEyebrowY
                                                            else
                                                            {
                                                                //OneFourthBottomRightEyelidX -> Punto 69, ID 107 coordenada X
                                                                if((ListaPuntos[69].DevolverPunto().X <= 0.023363) && (ListaPuntos[69].DevolverInt() == 107))
                                                                {
                                                                    //LeftBottomUpperLipY -> Punto 47, ID 82 coordenada Y
                                                                    if((ListaPuntos[47].DevolverPunto().Y <= -0.04902) && (ListaPuntos[47].DevolverInt() == 82))
                                                                    {
                                                                        //RightSideOfChinX -> Punto 15, ID 30 coordenada X
                                                                        if((ListaPuntos[15].DevolverPunto().X <= -0.036006) && (ListaPuntos[15].DevolverInt() == 30))
                                                                        {
                                                                            //Devolver Neutra, con 2 de exp
                                                                            int[] a = { 7, 2 };
                                                                            return a;
                                                                        }
                                                                        //Else del RightSideOfChinX
                                                                        else
                                                                        {
                                                                            //Devolver Alegria, con 96 de exp
                                                                            int[] a = { 1, 96 };
                                                                            return a;
                                                                        }
                                                                    }
                                                                    //Else del LeftBottomUpperLipY
                                                                    else
                                                                    {
                                                                        //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                                        if((ListaPuntos[0].DevolverPunto().Y <= 0.116484) && (ListaPuntos[0].DevolverInt() == 0))
                                                                        {
                                                                            //Devolver Enfado, con 2 de exp
                                                                            int[] a = { 4, 2 };
                                                                            return a;
                                                                        }
                                                                        //Else del TopSkullY
                                                                        else
                                                                        {
                                                                            //Devolver Sorpresa, con 2 de exp
                                                                            int[] a = { 3, 2 };
                                                                            return a;
                                                                        }
                                                                    }
                                                                }
                                                                //Else del OneFourthBottomRightEyelidX
                                                                else
                                                                {
                                                                    //OutsideRightCornerMouthZ -> Punto 16, ID 31 coordenada Z
                                                                    if((ListaPuntos[16].DevolverPunto().Z <= 0.061549) && (ListaPuntos[16].DevolverInt() == 31))
                                                                    {
                                                                        //MiddleTopDipUpperLipY -> Punto 2, ID 7 coordenada Y
                                                                        if((ListaPuntos[2].DevolverPunto().Y <= 0.083048) && (ListaPuntos[2].DevolverInt() == 7))
                                                                        {
                                                                            //Devolver Alegria, con 2 de exp
                                                                            int[] a = { 1, 2 };
                                                                            return a;
                                                                        }
                                                                        //Else del MiddleTopDipUpperLipY
                                                                        else
                                                                        {
                                                                            //Devolver Neutra, con 3 de exp
                                                                            int[] a = { 7, 3 };
                                                                            return a;
                                                                        }
                                                                    }
                                                                    //Else del OutsideRightCornerMouthZ
                                                                    else
                                                                    {
                                                                        //Devolver Tristeza, con 24 de exp
                                                                        int[] a = { 2, 24 };
                                                                        return a;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        //Else del LeftBottomUpperLipY
                                                        else
                                                        {
                                                            //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                                            if((ListaPuntos[31].DevolverPunto().Y <= 0.011846) && (ListaPuntos[31].DevolverInt() == 57))
                                                            {
                                                                //InnerBottomRightPupilY -> Punto 41, ID 72 coordenada Y
                                                                if((ListaPuntos[41].DevolverPunto().Y <= 0.01781) && (ListaPuntos[41].DevolverInt() == 72))
                                                                {
                                                                    //MiddleTopDipUpperLipY -> Punto 2, ID 7 coordenada Y
                                                                    if((ListaPuntos[2].DevolverPunto().Y <= 0.074601) && (ListaPuntos[2].DevolverInt() == 7))
                                                                    {
                                                                        //Devolver Enfado, con 36 de exp
                                                                        int[] a = { 4, 36 };
                                                                        return a;
                                                                    }
                                                                    //Else del OuterTopLeftPupilX
                                                                    else
                                                                    {
                                                                        //Devolver Alegria, con 4 de exp
                                                                        int[] a = { 1, 4 };
                                                                        return a;
                                                                    }
                                                                }
                                                                //Else del InnerBottomRightPupilY
                                                                else
                                                                {
                                                                    //Devolver Alegria, con 32|1
                                                                    int[] a = { 1, 31 };
                                                                    return a;
                                                                }
                                                            }
                                                            //Else del UnderMidBottomLeftEyelidY
                                                            else
                                                            {
                                                                //MiddleTopOfLeftEyebrowX -> Punto 23, ID 49 coordenada X
                                                                if((ListaPuntos[23].DevolverPunto().X <= 0.053997) && (ListaPuntos[23].DevolverInt() == 49))
                                                                {
                                                                    //Devolver Tristeza, con 2 de exp
                                                                    int[] a = { 2, 2 };
                                                                    return a;
                                                                }
                                                                //Else del MiddleTopOfLeftEyebrowX
                                                                else
                                                                {
                                                                    //Devolver Sorpresa, con 14 de exp
                                                                    int[] a = { 3, 14 };
                                                                    return a;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                //Else del RightOfChinZ
                                                else
                                                {
                                                    //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                    if((ListaPuntos[0].DevolverPunto().Y <= 0.113271) && (ListaPuntos[0].DevolverInt() == 0))
                                                    {
                                                        //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                        if((ListaPuntos[0].DevolverPunto().Y <= 0.10955) && (ListaPuntos[0].DevolverInt() == 0))
                                                        {
                                                            //Devolver Tristeza, con 18|1 de exp
                                                            int[] a = { 2, 17 };
                                                            return a;
                                                        }
                                                        //Else del TopSkullY
                                                        else
                                                        {
                                                            //Devolver Asco, con 79 de exp
                                                            int[] a = { 5, 79 };
                                                            return a;
                                                        }
                                                    }
                                                    //Else del TopSkullY
                                                    else
                                                    {
                                                        //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                                        if((ListaPuntos[31].DevolverPunto().Y <= 0.016032) && (ListaPuntos[31].DevolverInt() == 57))
                                                        {
                                                            //RightSideOfChinX -> Punto 15, ID 30 coordenada X
                                                            if((ListaPuntos[15].DevolverPunto().X <= -0.030943) && (ListaPuntos[15].DevolverInt() == 30))
                                                            {
                                                                //TopSkullY -> Punto 0, ID 0 coordenada Y
                                                                if((ListaPuntos[0].DevolverPunto().Y <= 0.122679) && (ListaPuntos[0].DevolverInt() == 0))
                                                                {
                                                                    //Devolver Alegria, con 73|1 de exp
                                                                    int[] a = { 1, 72 };
                                                                    return a;
                                                                }
                                                                //Else del TopSkullY
                                                                else
                                                                {
                                                                    //Devolver Sorpresa, con 32|1 de exp
                                                                    int[] a = { 3, 31 };
                                                                    return a;
                                                                }
                                                            }
                                                            //Else del RightSideOfChinX
                                                            else
                                                            {
                                                                //Devolver Enfado, con 17|1 de exp
                                                                int[] a = { 4, 16 };
                                                                return a;
                                                            }
                                                        }
                                                        //Else del UnderMidBottomLeftEyelidY
                                                        else
                                                        {
                                                            //Devolver Tristeza, con 16 de exp
                                                            int[] a = { 2, 16 };
                                                            return a;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        //Else del MiddleBottomOfRightEyebrowY
                                        else
                                        {
                                            //BelowThreeFourthLeftEyelidX -> Punto 64, ID 102 coordenada X
                                            if((ListaPuntos[64].DevolverPunto().X <= -0.037578) && (ListaPuntos[64].DevolverInt() == 102))
                                            {
                                                //MiddleTopOfRightEyebrowY -> Punto 6, ID 16 coordenada Y
                                                if((ListaPuntos[6].DevolverPunto().Y <= 0.032259) && (ListaPuntos[6].DevolverInt() == 16))
                                                {
                                                    //Devolver Alegria, con 17 de exp
                                                    int[] a = { 1, 17 };
                                                    return a;
                                                }
                                                //Else del MiddleTopOfRightEyebrowY
                                                else
                                                {
                                                    //LeftOfLeftEyebrowY -> Punto 22, ID 48 coordenada Y
                                                    if((ListaPuntos[22].DevolverPunto().Y <= -0.062819) && (ListaPuntos[22].DevolverInt() == 48))
                                                    {
                                                        //Devolver Tristeza, con 6|1 de exp
                                                        int[] a = { 2, 5 };
                                                        return a;
                                                    }
                                                    //Else del OuterTopLeftPupilX
                                                    else
                                                    {
                                                        //Devolver Sorpresa, con 39|1 de exp
                                                        int[] a = { 3, 38 };
                                                        return a;
                                                    }
                                                }
                                            }
                                            //Else del BelowThreeFourthLeftEyelidX
                                            else
                                            {
                                                //RightSideOfChinX -> Punto 15, ID 30 coordenada X
                                                if((ListaPuntos[15].DevolverPunto().X <= -0.02944) && (ListaPuntos[15].DevolverInt() == 30))
                                                {
                                                    //Devolver Enfado, con 75|1 de exp
                                                    int[] a = { 4, 74 };
                                                    return a;
                                                }
                                                //Else del RightSideOfChinX
                                                else
                                                {
                                                    //LeftSideOfCheekX -> Punto 32, ID 63 coordenada X
                                                    if((ListaPuntos[32].DevolverPunto().X <= 0.029136) && (ListaPuntos[32].DevolverInt() == 63))
                                                    {
                                                        //Devolver Asco, con 48|1 de exp
                                                        int[] a = { 5, 47 };
                                                        return a;
                                                    }
                                                    //Else del LeftSideOfCheekX
                                                    else
                                                    {
                                                        //Devolver Enfado, con 5 de exp
                                                        int[] a = { 4, 5 };
                                                        return a;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        //Else del MiddleBottomLowerLipZ
                        else
                        {
                            //RightSideOfChinX -> Punto 15, ID 30 coordenada X
                            if((ListaPuntos[15].DevolverPunto().X <= -0.031561) && (ListaPuntos[15].DevolverInt() == 30))
                            {
                                //UnderMidBottomLeftEyelidY -> Punto 31, ID 57 coordenada Y
                                if((ListaPuntos[31].DevolverPunto().Y <= -0.033112) && (ListaPuntos[31].DevolverInt() == 57))
                                {
                                    //MiddleBottomLowerLipZ -> Punto 21, ID 41 coordenada Z
                                    if((ListaPuntos[21].DevolverPunto().Z <= -0.033112) && (ListaPuntos[21].DevolverInt() == 41))
                                    {
                                        //Devolver Alegria, con 7 de exp
                                        int[] a = { 1, 7 };
                                        return a;
                                    }
                                    //Else del MiddleBottomLowerLipZ
                                    else
                                    {
                                        //Devolver Sorpresa, con 2 de exp
                                        int[] a = { 3, 2 };
                                        return a;
                                    }
                                }
                                //Else del UnderMidBottomLeftEyelidY
                                else
                                {
                                    //Devolver Sorpresa, con 128 de exp
                                    int[] a = { 3, 128 };
                                    return a;
                                }
                            }
                            //Else del RightSideOfChinX
                            else
                            {
                                //Devolver Enfado, con 14|1 de exp
                                int[] a = { 4, 13 };
                                return a;
                            }
                        }
                    }
                    //Else del OneFourthTopRightEyelidX
                    else
                    {
                        //BelowThreeFourthLeftEyelidX -> Punto 64, ID 102 coordenada X
                        if((ListaPuntos[64].DevolverPunto().X <= -0.048699) && (ListaPuntos[64].DevolverInt() == 102))
                        {
                            //AboveOneFourthLeftEyelidY -> Punto 66, ID 104 coordenada Y
                            if((ListaPuntos[66].DevolverPunto().Y <= 0.019726) && (ListaPuntos[66].DevolverInt() == 104))
                            {
                                //MiddleTopDipUpperLipY -> Punto 2, ID 7 coordenada Y
                                if((ListaPuntos[2].DevolverPunto().Y <= 0.090543) && (ListaPuntos[2].DevolverInt() == 7))
                                {
                                    //Devolver Alegria, con 7 de exp
                                    int[] a = { 1, 7 };
                                    return a;
                                }
                                //Else del OuterTopLeftPupilX
                                else
                                {
                                    //Devolver Enfado, con 4 de exp
                                    int[] a = { 4, 4 };
                                    return a;
                                }
                            }
                            //Else del AboveOneFourthLeftEyelidY
                            else
                            {
                                //Devolver Neutra, con 25 de exp
                                int[] a = { 7, 25 };
                                return a;
                            }
                        }
                        //Else del BelowThreeFourthLeftEyelidX
                        else
                        {
                            //UnderMidBottomRightEyelidY -> Punto 14, ID 24 coordenada Y
                            if((ListaPuntos[14].DevolverPunto().Y <= 0.011903) && (ListaPuntos[14].DevolverInt() == 24))
                            {
                                //OuterCornerOfLeftEyeX -> Punto 27, ID 53 coordenada X
                                if((ListaPuntos[27].DevolverPunto().X <= 0.03608) && (ListaPuntos[27].DevolverInt() == 53))
                                {
                                    //Devolver Asco, con 5|1 de exp
                                    int[] a = { 5, 4 };
                                    return a;
                                }
                                //Else del OuterCornerOfLeftEyeX
                                else
                                {
                                    //Devolver Neutra, con 5|1 de exp
                                    int[] a = { 7, 4 };
                                    return a;
                                }
                            }
                            //Else del UnderMidBottomRightEyelidY
                            else
                            {
                                //Devolver Alegria, con 517|7
                                int[] a = { 1, 517 };
                                return a;
                            }
                        }
                    }
                }
            }
            //Else del OuterTopLeftPupilX
            else
            {
                //UnderMidBottomLeftEyelidX -> Punto 31, ID 57 coordenada X
                if((ListaPuntos[31].DevolverPunto().X <= 0.015812) && (ListaPuntos[31].DevolverInt() == 57))
                {
                    //RightOfChinX -> Punto 17, ID 32 coordenada X
                    if((ListaPuntos[17].DevolverPunto().X <= -0.027578) && (ListaPuntos[17].DevolverInt() == 32))
                    {
                        //Devolver Asco, con 14 de exp
                        int[] a = { 5, 14 };
                        return a;
                    }
                    //Else del RightOfChinX
                    else
                    {
                        //AboveThreeFourthRightEyelidY -> Punto 57, ID 95 coordenada Y
                        if((ListaPuntos[57].DevolverPunto().Y <= -0.044333) && (ListaPuntos[57].DevolverInt() == 95))
                        {
                            //Devolver Miedo, con 400 de exp
                            int[] a = { 6, 400 };
                            return a;
                        }
                        //Else del AboveThreeFourthRightEyelidY
                        else
                        {
                            //MiddleBottomRightEyelidY -> Punto 12, ID 22 coordenada Y
                            if((ListaPuntos[12].DevolverPunto().Y <= 0.012032) && (ListaPuntos[12].DevolverInt() == 22))
                            {
                                //Devolver Sorpresa, con 4|1 de exp
                                int[] a = { 3, 3 };
                                return a;
                            }
                            //Else del MiddleBottomRightEyelidY
                            else
                            {
                                //OuterCornerOfRightEyeY -> Punto 10, ID 20 coordenada Y
                                if((ListaPuntos[10].DevolverPunto().Y <= 0.016271) && (ListaPuntos[10].DevolverInt() == 20))
                                {
                                    //Devolver Tristeza, con 4 de exp
                                    int[] a = { 2, 4 };
                                    return a;
                                }
                                //Else del OuterTopLeftPupilX
                                else
                                {
                                    //Devolver Enfado, con 2 de exp
                                    int[] a = { 4, 2 };
                                    return a;
                                }
                            }
                        }
                    }
                }
                //Else del UnderMidBottomLeftEyelidX
                else
                {
                    //MiddleBottomOfLeftEyebrowY -> Punto 25, ID 51 coordenada Y
                    if((ListaPuntos[25].DevolverPunto().Y <= 0.020195) && (ListaPuntos[25].DevolverInt() == 51))
                    {
                        //ThreeFourthBottomRightEyelidY -> Punto 61, ID 99 coordenada Y
                        if((ListaPuntos[61].DevolverPunto().Y <= 0.010662) && (ListaPuntos[61].DevolverInt() == 99))
                        {
                            //Devolver Alegria, con 13 de exp
                            int[] a = { 1, 13 };
                            return a;
                        }
                        //Else del OuterTopLeftPupilX
                        else
                        {
                            //Devolver Sorpresa, con 6 de exp
                            int[] a = { 3, 6 };
                            return a;
                        }
                    }
                    //Else del MiddleBottomOfLeftEyebrowY
                    else
                    {
                        //Devolver Enfado, con 9 de exp
                        int[] a = { 4, 9 };
                        return a;
                    }
                }
            }

        }

        //   Metodo para guardar la imagen que recoge la Kinect, aunque la almacene en la direccion del proyecto
        //   ->  FaceTrackingBasics-WPF\bin\x86\Debug
        void CreateThumbnail(string filename, BitmapSource image5)
        {
            if (filename != string.Empty)
            {
                using (FileStream stream5 = new FileStream(filename, FileMode.Create))
                {
                    PngBitmapEncoder encoder5 = new PngBitmapEncoder();
                    encoder5.Frames.Add(BitmapFrame.Create(image5));
                    encoder5.Save(stream5);
                    stream5.Close();
                }
            }

            //Mover la imagen al directorio bueno
            string sourceFile = @"C:\Users\Fernando\Desktop\Face traking\FaceTrackingBasics-WPF\bin\x86\Debug\" + filename;
            string destinationFile = @"C:\Base De Datos\" + filename;

            if (!File.Exists(sourceFile))
            {
                // This statement ensures that the file is created,
                // but the handle is not kept.
                using (FileStream fs = File.Create(sourceFile)) { }
            }

            // Ensure that the target does not exist.
            if (File.Exists(destinationFile))
                File.Delete(destinationFile);

            //Moverlo si existe en el lugar de origen, y no existe en el lugar de destino
            if (File.Exists(sourceFile) && !File.Exists(destinationFile))
            {
                // Move the file.
                File.Move(sourceFile, destinationFile);
                Console.WriteLine("{0} was moved to {1}.", sourceFile, destinationFile);
            }


            // See if the original exists now.
            if (File.Exists(sourceFile))
            {
                Console.WriteLine("The original file still exists, which is unexpected.");
            }
            else
            {
                Console.WriteLine("The original file no longer exists, which is expected.");
            }
        }

        //Metodo para realizar las grabaciones
        private void Grabar_Click(object sender, RoutedEventArgs e)
        {


            this.video = !this.video;
            this.Comenzar_A_Contar = !this.Comenzar_A_Contar;

            if (video)
            {
                LabelInformativoGrabacion.Content = "Grabando";
            }
            else
            {
                LabelInformativoGrabacion.Content = "Sin grabar";

                string result = this.EmitirResultados();

                //Si se para de grabar se tiene que devolver el estudio estadisitico que se realizo del video
                using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"C:\Base De Datos\Script completo para Weka y Mattlab\Resultados.txt", true))
                {
                    file1.WriteLine(result);
                }
            }
        }


        //Reinico de las estadisticas
        private void Reincio_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i <= 6; i++)
            {
                this.EmocionesTotales[i,0] = 0;
                this.EmocionesTotalesEtiquetada[i,0] = 0;
            }
        }

        private void IsCheckedAlegria(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Alegria";
        }

        private void IsCheckedTristeza(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Tristeza";
        }

        private void IsCheckedEnfado(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Enfado";
        }

        private void IsCheckedAsco(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Asco";
        }

        private void IsCheckedMiedo(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Miedo";
        }

        private void IsCheckedSorpresa(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Sorpresa";
        }

        private void IsCheckedNeutra(object sender, RoutedEventArgs e)
        {
            EtiquetaEmocion = "Neutra";
        }


        private void Vaciar_Vector_Emociones(int[,] ListaEmociones)
        {
            for(int i = 0; i<=6; i++)
            {
                //Cantidad de cada emocion
                ListaEmociones[i,0] = 0;

                //Experiencia acumulada
                ListaEmociones[i,1] = 0;
            }

        }


        private int Cantidad_Vector_Emociones(int[,] ListaEmociones)
        {
            int res = 0 + ListaEmociones[0, 0] + ListaEmociones[1, 0] + ListaEmociones[2, 0] + ListaEmociones[3, 0] + ListaEmociones[4, 0] + ListaEmociones[5, 0] + ListaEmociones[6, 0];

            return res;
        }


        private int Emocion_Escogida()
        {
            int ResEmocion = 0;
            int CantEmocion = 0;

            //Funcion heuristica para preparar los resultados
            int[] Heuristica = new int[7];

            for (int i = 0; i <= 6; i++)
            {
                Heuristica[i] = this.Emociones[i, 0];
            }


            //Se toma la opcion mayoritaria
            for (int i = 0; i <= 6; i++)
            {
                if (CantEmocion < Heuristica[i])
                {
                    CantEmocion = Heuristica[i];
                    ResEmocion = i;
                }
            }

            return ResEmocion;
        }

        private int Emocion_Escogida_Total(int[,] ListaEmociones)
        {
            //Lista de emociones
            //  1 -> Alegria
            //  2 -> Tristeza
            //  3 -> Sorpresa
            //  4 -> Enfado
            //  5 -> Asco
            //  6 -> Miedo
            //  7 -> Neutra

            int CantEmocion = 0;

            if (this.EtiquetaEmocion.Equals("Alegria"))
            {
                CantEmocion = ListaEmociones[0, 0];
            }

            if (this.EtiquetaEmocion.Equals("Tristeza"))
            {
                CantEmocion = ListaEmociones[1, 0];
            }
            if (this.EtiquetaEmocion.Equals("Sorpresa"))
            {
                CantEmocion = ListaEmociones[2, 0];
            }
            if (this.EtiquetaEmocion.Equals("Enfado"))
            {
                CantEmocion = ListaEmociones[3, 0];
            }
            if (this.EtiquetaEmocion.Equals("Asco"))
            {
                CantEmocion = ListaEmociones[4, 0];
            }
            if (this.EtiquetaEmocion.Equals("Miedo"))
            {
                CantEmocion = ListaEmociones[5, 0];
            }
            if (this.EtiquetaEmocion.Equals("Neutra"))
            {
                CantEmocion = ListaEmociones[6, 0];
            }
            return CantEmocion;
        }



        private string EmitirResultados()
        {            
            float Escogida = this.Emocion_Escogida_Total(this.EmocionesTotales);
            float Total = this.Cantidad_Vector_Emociones(this.EmocionesTotales);

            float Porcentaje = (  Escogida / Total  ) * 100;

            string Resultados = "Emocion etiquetada: " + this.EtiquetaEmocion + " -- Porcentaje de acierto -> " + Porcentaje + "%";

            return Resultados;
        }


        private void ImprimirEstadisiticas()
        {
            //Lista de emociones
            //  0 -> Alegria
            //  1 -> Tristeza
            //  2 -> Sorpresa
            //  3 -> Enfado
            //  4 -> Asco
            //  5 -> Miedo
            //  6 -> Neutra

            //Emocion Total
            float Total = this.Cantidad_Vector_Emociones(this.EmocionesTotales);

            //Alegria
            float AlegriaTotal = this.EmocionesTotales[0, 0];
            float PorcentajeAlegria = (AlegriaTotal / Total) * 100;
            this.label_Porcentaje_Alegria.Content = PorcentajeAlegria+"%";

            //Tristeza
            float TristezaTotal = this.EmocionesTotales[1, 0];
            float PorcentajeTristeza = (TristezaTotal / Total) * 100;
            this.label_PorcentajeTristeza.Content = PorcentajeTristeza + "%";

            //Sorpresa
            float SorpresaTotal = this.EmocionesTotales[2, 0];
            float PorcentajeSorpresa = (SorpresaTotal / Total) * 100;
            this.label_PorcentajeSorpresa.Content = PorcentajeSorpresa + "%";

            //Enfado
            float EnfadoTotal = this.EmocionesTotales[3, 0];
            float PorcentajeEnfado = (EnfadoTotal / Total) * 100;
            this.label_PorcentajeEnfado.Content = PorcentajeEnfado + "%";

            //Asco
            float AscoTotal = this.EmocionesTotales[4, 0];
            float PorcentajeAsco = (AscoTotal / Total) * 100;
            this.label_PorcentajeAsco.Content = PorcentajeAsco + "%";

            //Miedo
            float MiedoTotal = this.EmocionesTotales[5, 0];
            float PorcentajeMiedo = (MiedoTotal / Total) * 100;
            this.label_PorcentajeMiedo.Content = PorcentajeMiedo + "%";

            //Neutra
            float NeutraTotal = this.EmocionesTotales[6, 0];
            float PorcentajeNeutra = (NeutraTotal / Total) * 100;
            this.label_PorcentajeNeutra.Content = PorcentajeNeutra + "%";



            //Emocion TotalEtiquetado
            float TotalEtiquetada = this.Cantidad_Vector_Emociones(this.EmocionesTotalesEtiquetada);


            //Alegria
            float AlegriaTotalEtiquetada = this.EmocionesTotalesEtiquetada[0, 0];
            float PorcentajeAlegriaEtiquetada = (AlegriaTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_Porcentaje_AlegriaEtiquetado.Content = PorcentajeAlegriaEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Alegria"))
            {
                this.label_Porcentaje_AlegriaEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_Porcentaje_AlegriaEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }



            //Tristeza
            float TristezaTotalEtiquetada = this.EmocionesTotalesEtiquetada[1, 0];
            float PorcentajeTristezaEtiquetada = (TristezaTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeTristezaEtiquetado.Content = PorcentajeTristezaEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Tristeza"))
            {
                this.label_PorcentajeTristezaEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeTristezaEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }



            //Sorpresa
            float SorpresaTotalEtiquetada = this.EmocionesTotalesEtiquetada[2, 0];
            float PorcentajeSorpresaEtiquetada = (SorpresaTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeSorpresaEtiquetado.Content = PorcentajeSorpresaEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Sorpresa"))
            {
                this.label_PorcentajeSorpresaEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeSorpresaEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }


            //Enfado
            float EnfadoTotalEtiquetada = this.EmocionesTotalesEtiquetada[3, 0];
            float PorcentajeEnfadoEtiquetada = (EnfadoTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeEnfadoEtiquetado.Content = PorcentajeEnfadoEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Enfado"))
            {
                this.label_PorcentajeEnfadoEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeEnfadoEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }



            //Asco
            float AscoTotalEtiquetada = this.EmocionesTotalesEtiquetada[4, 0];
            float PorcentajeAscoEtiquetada = (AscoTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeAscoEtiquetado.Content = PorcentajeAscoEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Asco"))
            {
                this.label_PorcentajeAscoEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeAscoEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }



            //Miedo
            float MiedoTotalEtiquetada = this.EmocionesTotalesEtiquetada[5, 0];
            float PorcentajeMiedoEtiquetada = (MiedoTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeMiedoEtiquetado.Content = PorcentajeMiedoEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Miedo"))
            {
                this.label_PorcentajeMiedoEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeMiedoEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }


            //NeutraEtiquetada
            float NeutraTotalEtiquetada = this.EmocionesTotalesEtiquetada[6, 0];
            float PorcentajeNeutraEtiquetada = (NeutraTotalEtiquetada / TotalEtiquetada) * 100;
            this.label_PorcentajeNeutraEtiquetado.Content = PorcentajeNeutraEtiquetada + "%";
            //Ponerla en verde o en verde si es la etiquetada o no
            if (this.EtiquetaEmocion.Equals("Neutra"))
            {
                this.label_PorcentajeNeutraEtiquetado.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                this.label_PorcentajeNeutraEtiquetado.Foreground = System.Windows.Media.Brushes.Red;
            }


        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Arbol dlg = new Arbol();

            dlg.Owner = this;

            dlg.ShowDialog();

            string Arboles = dlg.res;

            for (int i = 0; i < Arboles.Length; i++)
            {
                ArbolesActivos[i] = Arboles[i]-48;
            }

        }

        //Metodo para Desactivar los arboles de decision
        private void DesactivarArboles(int[] Arboles)
        {
            for (int i = 0; i < Arboles.Length; i++)
            {
                Arboles[i] = 0;
            }
        }

        //Metodo para rellenar los puntos de la cara
        private void GenerarListaDePuntos()
        {

            //Lista donde aparecen los puntos principales de la matriz de las caras          
            PuntoNombrado Punto0 = new PuntoNombrado();
            Punto0.ModificarIntegerData(0);
            Punto0.ModificarStringData("TopSkull");
            ListaPuntos.Add(Punto0);

            PuntoNombrado Punto1 = new PuntoNombrado();
            Punto1.ModificarIntegerData(1);
            Punto1.ModificarStringData("TopRightForehead");
            ListaPuntos.Add(Punto1);

            PuntoNombrado Punto7 = new PuntoNombrado();
            Punto7.ModificarIntegerData(7);
            Punto7.ModificarStringData("MiddleTopDipUpperLip");
            ListaPuntos.Add(Punto7);

            PuntoNombrado Punto9 = new PuntoNombrado();
            Punto9.ModificarIntegerData(9);
            Punto9.ModificarStringData("AboveChin");
            ListaPuntos.Add(Punto9);


            PuntoNombrado Punto10 = new PuntoNombrado();
            Punto10.ModificarIntegerData(10);
            Punto10.ModificarStringData("BottomOfChin");
            ListaPuntos.Add(Punto10);


            PuntoNombrado Punto15 = new PuntoNombrado();
            Punto15.ModificarIntegerData(15);
            Punto15.ModificarStringData("RightOfRightEyebrow");
            ListaPuntos.Add(Punto15);


            PuntoNombrado Punto16 = new PuntoNombrado();
            Punto16.ModificarIntegerData(16);
            Punto16.ModificarStringData("MiddleTopOfRightEyebrow");
            ListaPuntos.Add(Punto16);

            PuntoNombrado Punto17 = new PuntoNombrado();
            Punto17.ModificarIntegerData(17);
            Punto17.ModificarStringData("LeftOfRightEyebrow");
            ListaPuntos.Add(Punto17);


            PuntoNombrado Punto18 = new PuntoNombrado();
            Punto18.ModificarIntegerData(18);
            Punto18.ModificarStringData("MiddleBottomOfRightEyebrow");
            ListaPuntos.Add(Punto18);

            PuntoNombrado Punto19 = new PuntoNombrado();
            Punto19.ModificarIntegerData(19);
            Punto19.ModificarStringData("AboveMidUpperRightEyelid");
            ListaPuntos.Add(Punto19);

            PuntoNombrado Punto20 = new PuntoNombrado();
            Punto20.ModificarIntegerData(20);
            Punto20.ModificarStringData("OuterCornerOfRightEye");
            ListaPuntos.Add(Punto20);

            PuntoNombrado Punto21 = new PuntoNombrado();
            Punto21.ModificarIntegerData(21);
            Punto21.ModificarStringData("MiddleTopRightEyelid");
            ListaPuntos.Add(Punto21);

            PuntoNombrado Punto22 = new PuntoNombrado();
            Punto22.ModificarIntegerData(22);
            Punto22.ModificarStringData("MiddleBottomRightEyelid");
            ListaPuntos.Add(Punto22);


            PuntoNombrado Punto23 = new PuntoNombrado();
            Punto23.ModificarIntegerData(23);
            Punto23.ModificarStringData("InnerCornerRightEye");
            ListaPuntos.Add(Punto23);

            PuntoNombrado Punto24 = new PuntoNombrado();
            Punto24.ModificarIntegerData(24);
            Punto24.ModificarStringData("UnderMidBottomRightEyelid");
            ListaPuntos.Add(Punto24);


            PuntoNombrado Punto30 = new PuntoNombrado();
            Punto30.ModificarIntegerData(30);
            Punto30.ModificarStringData("RightSideOfChin");
            ListaPuntos.Add(Punto30);


            PuntoNombrado Punto31 = new PuntoNombrado();
            Punto31.ModificarIntegerData(31);
            Punto31.ModificarStringData("OutsideRightCornerMouth");
            ListaPuntos.Add(Punto31);

            PuntoNombrado Punto32 = new PuntoNombrado();
            Punto32.ModificarIntegerData(32);
            Punto32.ModificarStringData("RightOfChin");
            ListaPuntos.Add(Punto32);


            PuntoNombrado Punto33 = new PuntoNombrado();
            Punto33.ModificarIntegerData(33);
            Punto33.ModificarStringData("RightTopDipUpperLip");
            ListaPuntos.Add(Punto33);


            PuntoNombrado Punto34 = new PuntoNombrado();
            Punto34.ModificarIntegerData(34);
            Punto34.ModificarStringData("TopLeftForehead");
            ListaPuntos.Add(Punto34);


            PuntoNombrado Punto40 = new PuntoNombrado();
            Punto40.ModificarIntegerData(40);
            Punto40.ModificarStringData("MiddleTopLowerLip");
            ListaPuntos.Add(Punto40);


            PuntoNombrado Punto41 = new PuntoNombrado();
            Punto41.ModificarIntegerData(41);
            Punto41.ModificarStringData("MiddleBottomLowerLip");
            ListaPuntos.Add(Punto41);


            PuntoNombrado Punto48 = new PuntoNombrado();
            Punto48.ModificarIntegerData(48);
            Punto48.ModificarStringData("LeftOfLeftEyebrow");
            ListaPuntos.Add(Punto48);


            PuntoNombrado Punto49 = new PuntoNombrado();
            Punto49.ModificarIntegerData(49);
            Punto49.ModificarStringData("MiddleTopOfLeftEyebrow");
            ListaPuntos.Add(Punto49);


            PuntoNombrado Punto50 = new PuntoNombrado();
            Punto50.ModificarIntegerData(50);
            Punto50.ModificarStringData("RightOfLeftEyebrow");
            ListaPuntos.Add(Punto50);


            PuntoNombrado Punto51 = new PuntoNombrado();
            Punto51.ModificarIntegerData(51);
            Punto51.ModificarStringData("MiddleBottomOfLeftEyebrow");
            ListaPuntos.Add(Punto51);


            PuntoNombrado Punto52 = new PuntoNombrado();
            Punto52.ModificarIntegerData(52);
            Punto52.ModificarStringData("AboveMidUpperLeftEyelid");
            ListaPuntos.Add(Punto52);


            PuntoNombrado Punto53 = new PuntoNombrado();
            Punto53.ModificarIntegerData(53);
            Punto53.ModificarStringData("OuterCornerOfLeftEye");
            ListaPuntos.Add(Punto53);


            PuntoNombrado Punto54 = new PuntoNombrado();
            Punto54.ModificarIntegerData(54);
            Punto54.ModificarStringData("MiddleTopLeftEyelid");
            ListaPuntos.Add(Punto54);


            PuntoNombrado Punto55 = new PuntoNombrado();
            Punto55.ModificarIntegerData(55);
            Punto55.ModificarStringData("MiddleBottomLeftEyelid");
            ListaPuntos.Add(Punto55);


            PuntoNombrado Punto56 = new PuntoNombrado();
            Punto56.ModificarIntegerData(56);
            Punto56.ModificarStringData("InnerCornerLeftEye");
            ListaPuntos.Add(Punto56);


            PuntoNombrado Punto57 = new PuntoNombrado();
            Punto57.ModificarIntegerData(57);
            Punto57.ModificarStringData("UnderMidBottomLeftEyelid");
            ListaPuntos.Add(Punto57);


            PuntoNombrado Punto63 = new PuntoNombrado();
            Punto63.ModificarIntegerData(63);
            Punto63.ModificarStringData("LeftSideOfCheek");
            ListaPuntos.Add(Punto63);


            PuntoNombrado Punto64 = new PuntoNombrado();
            Punto64.ModificarIntegerData(64);
            Punto64.ModificarStringData("OutsideLeftCornerMouth");
            ListaPuntos.Add(Punto64);


            PuntoNombrado Punto65 = new PuntoNombrado();
            Punto65.ModificarIntegerData(65);
            Punto65.ModificarStringData("LeftOfChin");
            ListaPuntos.Add(Punto65);


            PuntoNombrado Punto66 = new PuntoNombrado();
            Punto66.ModificarIntegerData(66);
            Punto66.ModificarStringData("LeftTopDipUpperLip");
            ListaPuntos.Add(Punto66);


            PuntoNombrado Punto67 = new PuntoNombrado();
            Punto67.ModificarIntegerData(67);
            Punto67.ModificarStringData("OuterTopRightPupil");
            ListaPuntos.Add(Punto67);


            PuntoNombrado Punto68 = new PuntoNombrado();
            Punto68.ModificarIntegerData(68);
            Punto68.ModificarStringData("OuterBottomRightPupil");
            ListaPuntos.Add(Punto68);


            PuntoNombrado Punto69 = new PuntoNombrado();
            Punto69.ModificarIntegerData(69);
            Punto69.ModificarStringData("OuterTopLeftPupil");
            ListaPuntos.Add(Punto69);


            PuntoNombrado Punto70 = new PuntoNombrado();
            Punto70.ModificarIntegerData(70);
            Punto70.ModificarStringData("OuterBottomLeftPupil");
            ListaPuntos.Add(Punto70);


            PuntoNombrado Punto71 = new PuntoNombrado();
            Punto71.ModificarIntegerData(71);
            Punto71.ModificarStringData("InnerTopRightPupil");
            ListaPuntos.Add(Punto71);


            PuntoNombrado Punto72 = new PuntoNombrado();
            Punto72.ModificarIntegerData(72);
            Punto72.ModificarStringData("InnerBottomRightPupil");
            ListaPuntos.Add(Punto72);


            PuntoNombrado Punto73 = new PuntoNombrado();
            Punto73.ModificarIntegerData(73);
            Punto73.ModificarStringData("InnerTopLeftPupil");
            ListaPuntos.Add(Punto73);


            PuntoNombrado Punto74 = new PuntoNombrado();
            Punto74.ModificarIntegerData(74);
            Punto74.ModificarStringData("InnerBottomLeftPupil");
            ListaPuntos.Add(Punto74);


            PuntoNombrado Punto79 = new PuntoNombrado();
            Punto79.ModificarIntegerData(79);
            Punto79.ModificarStringData("RightTopUpperLip");
            ListaPuntos.Add(Punto79);


            PuntoNombrado Punto80 = new PuntoNombrado();
            Punto80.ModificarIntegerData(80);
            Punto80.ModificarStringData("LeftTopUpperLip");
            ListaPuntos.Add(Punto80);


            PuntoNombrado Punto81 = new PuntoNombrado();
            Punto81.ModificarIntegerData(81);
            Punto81.ModificarStringData("RightBottomUpperLip");
            ListaPuntos.Add(Punto81);


            PuntoNombrado Punto82 = new PuntoNombrado();
            Punto82.ModificarIntegerData(82);
            Punto82.ModificarStringData("LeftBottomUpperLip");
            ListaPuntos.Add(Punto82);


            PuntoNombrado Punto83 = new PuntoNombrado();
            Punto83.ModificarIntegerData(83);
            Punto83.ModificarStringData("RightTopLowerLip");
            ListaPuntos.Add(Punto83);


            PuntoNombrado Punto84 = new PuntoNombrado();
            Punto84.ModificarIntegerData(84);
            Punto84.ModificarStringData("LeftTopLowerLip");
            ListaPuntos.Add(Punto84);


            PuntoNombrado Punto85 = new PuntoNombrado();
            Punto85.ModificarIntegerData(85);
            Punto85.ModificarStringData("RightBottomLowerLip");
            ListaPuntos.Add(Punto85);


            PuntoNombrado Punto86 = new PuntoNombrado();
            Punto86.ModificarIntegerData(86);
            Punto86.ModificarStringData("LeftBottomLowerLip");
            ListaPuntos.Add(Punto86);


            PuntoNombrado Punto87 = new PuntoNombrado();
            Punto87.ModificarIntegerData(87);
            Punto87.ModificarStringData("MiddleBottomUpperLip");
            ListaPuntos.Add(Punto87);


            PuntoNombrado Punto88 = new PuntoNombrado();
            Punto88.ModificarIntegerData(88);
            Punto88.ModificarStringData("LeftCornerMouth");
            ListaPuntos.Add(Punto88);


            PuntoNombrado Punto89 = new PuntoNombrado();
            Punto89.ModificarIntegerData(89);
            Punto89.ModificarStringData("RightCornerMouth");
            ListaPuntos.Add(Punto89);


            PuntoNombrado Punto90 = new PuntoNombrado();
            Punto90.ModificarIntegerData(90);
            Punto90.ModificarStringData("BottomOfRightCheek");
            ListaPuntos.Add(Punto90);


            PuntoNombrado Punto91 = new PuntoNombrado();
            Punto91.ModificarIntegerData(91);
            Punto91.ModificarStringData("BottomOfLeftCheek");
            ListaPuntos.Add(Punto91);


            PuntoNombrado Punto95 = new PuntoNombrado();
            Punto95.ModificarIntegerData(95);
            Punto95.ModificarStringData("AboveThreeFourthRightEyelid");
            ListaPuntos.Add(Punto95);


            PuntoNombrado Punto96 = new PuntoNombrado();
            Punto96.ModificarIntegerData(96);
            Punto96.ModificarStringData("AboveThreeFourthLeftEyelid");
            ListaPuntos.Add(Punto96);


            PuntoNombrado Punto97 = new PuntoNombrado();
            Punto97.ModificarIntegerData(97);
            Punto97.ModificarStringData("ThreeFourthTopRightEyelid");
            ListaPuntos.Add(Punto97);


            PuntoNombrado Punto98 = new PuntoNombrado();
            Punto98.ModificarIntegerData(98);
            Punto98.ModificarStringData("ThreeFourthTopLeftEyelid");
            ListaPuntos.Add(Punto98);

            PuntoNombrado Punto99 = new PuntoNombrado();
            Punto99.ModificarIntegerData(99);
            Punto99.ModificarStringData("ThreeFourthBottomRightEyelid");
            ListaPuntos.Add(Punto99);


            PuntoNombrado Punto100 = new PuntoNombrado();
            Punto100.ModificarIntegerData(100);
            Punto100.ModificarStringData("ThreeFourthBottomLeftEyelid");
            ListaPuntos.Add(Punto100);



            PuntoNombrado Punto101 = new PuntoNombrado();
            Punto101.ModificarIntegerData(101);
            Punto101.ModificarStringData("BelowThreeFourthRightEyelid");
            ListaPuntos.Add(Punto101);


            PuntoNombrado Punto102 = new PuntoNombrado();
            Punto102.ModificarIntegerData(102);
            Punto102.ModificarStringData("BelowThreeFourthLeftEyelid");
            ListaPuntos.Add(Punto102);


            PuntoNombrado Punto103 = new PuntoNombrado();
            Punto103.ModificarIntegerData(103);
            Punto103.ModificarStringData("AboveOneFourthRightEyelid");
            ListaPuntos.Add(Punto103);


            PuntoNombrado Punto104 = new PuntoNombrado();
            Punto104.ModificarIntegerData(104);
            Punto104.ModificarStringData("AboveOneFourthLeftEyelid");
            ListaPuntos.Add(Punto104);


            PuntoNombrado Punto105 = new PuntoNombrado();
            Punto105.ModificarIntegerData(105);
            Punto105.ModificarStringData("OneFourthTopRightEyelid");
            ListaPuntos.Add(Punto105);


            PuntoNombrado Punto106 = new PuntoNombrado();
            Punto106.ModificarIntegerData(106);
            Punto106.ModificarStringData("OneFourthTopLeftEyelid");
            ListaPuntos.Add(Punto106);


            PuntoNombrado Punto107 = new PuntoNombrado();
            Punto107.ModificarIntegerData(107);
            Punto107.ModificarStringData("OneFourthBottomRightEyelid");
            ListaPuntos.Add(Punto107);


            PuntoNombrado Punto108 = new PuntoNombrado();
            Punto108.ModificarIntegerData(108);
            Punto108.ModificarStringData("OneFourthBottomLeftEyelid");
            ListaPuntos.Add(Punto108);


            PuntoNombrado Punto94 = new PuntoNombrado();
            Punto94.ModificarIntegerData(94);
            Punto94.ModificarStringData("PuntoNariz");
            ListaPuntos.Add(Punto94);

        }


        // Metodo empleado para convertir la Lista de puntos nombrados, en un array de doubles, que
        // es lo que se necesita para los arboles generados de forma automatica por WEKA
        private double[] conversion(List<PuntoNombrado> PuntosCara)
        {
            double[] lista = new double[213];

            for (int i = 0; i < 71; i++)
            {
                int id = i*3;
                lista[id] = (double) PuntosCara[i].DevolverPunto().X;
                lista[id + 1] = (double) PuntosCara[i].DevolverPunto().Y;
                lista[id + 2] = (double) PuntosCara[i].DevolverPunto().Z;
            }

            string Solista = "";
            for (int i = 0; i < lista.Length; i++)
            {
                Solista = Solista + lista[i] + " ; ";
            }

            string path = "C:\\Base De Datos\\Script completo para Weka y Mattlab\\prueba.txt";

            using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(path, true))
            {
                file1.WriteLine(Solista);
            }

            return lista;
        }


        private void IniciarArchivosDeTexto()
        {
            //Primero hay que comprobar si ya existe el fichero
            string pathExistTopSkull = "C:\\Base De Datos\\Script completo para Weka y Mattlab\\BBDDEmocionesTopSkull.txt";
            string pathExistNariz = "C:\\Base De Datos\\Script completo para Weka y Mattlab\\BBDDEmocionesNariz.txt";
            string pathSumario = @"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt";

            //Comprobacion
            if (System.IO.File.Exists(pathExistTopSkull) && System.IO.File.Exists(pathExistNariz) && System.IO.File.Exists(pathSumario))
            {
                //Si existe se activa el booleano de BBDD
                BBDDExiste = true;
            }
            else
            {
                //Si no existe se inicializa con los valores por defecto para el formato AFFR de Weka
                string[] lineaPrincipal = {       "@relation Emociones",
                                                  "@attribute Fotograma string",
                                                  "@attribute TopSkullX real",
                                                  "@attribute TopSkullY real",
                                                  "@attribute TopSkullZ real",
                                                  "@attribute TopRightForeheadX real",
                                                  "@attribute TopRightForeheadY real",
                                                  "@attribute TopRightForeheadZ real",
                                                  "@attribute MiddleTopDipUpperLipX real",
                                                  "@attribute MiddleTopDipUpperLipY real",
                                                  "@attribute MiddleTopDipUpperLipZ real",
                                                  "@attribute AboveChinX real",
                                                  "@attribute AboveChinY real",
                                                  "@attribute AboveChinZ real",
                                                  "@attribute BottomOfChinX real",
                                                  "@attribute BottomOfChinY real",
                                                  "@attribute BottomOfChinZ real",
                                                  "@attribute RightOfRightEyebrowX real",
                                                  "@attribute RightOfRightEyebrowY real",
                                                  "@attribute RightOfRightEyebrowZ real",
                                                  "@attribute MiddleTopOfRightEyebrowX real",
                                                  "@attribute MiddleTopOfRightEyebrowY real",
                                                  "@attribute MiddleTopOfRightEyebrowZ real",
                                                  "@attribute LeftOfRightEyebrowX real",
                                                  "@attribute LeftOfRightEyebrowY real",
                                                  "@attribute LeftOfRightEyebrowZ real",
                                                  "@attribute MiddleBottomOfRightEyebrowX real",
                                                  "@attribute MiddleBottomOfRightEyebrowY real",
                                                  "@attribute MiddleBottomOfRightEyebrowZ real",
                                                  "@attribute AboveMidUpperRightEyelidX real",
                                                  "@attribute AboveMidUpperRightEyelidY real",
                                                  "@attribute AboveMidUpperRightEyelidZ real",
                                                  "@attribute OuterCornerOfRightEyeX real",
                                                  "@attribute OuterCornerOfRightEyeY real", 
                                                  "@attribute OuterCornerOfRightEyeZ real",
                                                  "@attribute MiddleTopRightEyelidX real",
                                                  "@attribute MiddleTopRightEyelidY real",
                                                  "@attribute MiddleTopRightEyelidZ real",
                                                  "@attribute MiddleBottomRightEyelidX real",
                                                  "@attribute MiddleBottomRightEyelidY real",
                                                  "@attribute MiddleBottomRightEyelidZ real",
                                                  "@attribute InnerCornerRightEyeX real",
                                                  "@attribute InnerCornerRightEyeY real",
                                                  "@attribute InnerCornerRightEyeZ real",
                                                  "@attribute UnderMidBottomRightEyelidX real",
                                                  "@attribute UnderMidBottomRightEyelidY real",
                                                  "@attribute UnderMidBottomRightEyelidZ real",
                                                  "@attribute RightSideOfChinX real",
                                                  "@attribute RightSideOfChinY real",
                                                  "@attribute RightSideOfChinZ real",
                                                  "@attribute OutsideRightCornerMouthX real",
                                                  "@attribute OutsideRightCornerMouthY real",
                                                  "@attribute OutsideRightCornerMouthZ real",
                                                  "@attribute RightOfChinX real",
                                                  "@attribute RightOfChinY real",
                                                  "@attribute RightOfChinZ real",
                                                  "@attribute RightTopDipUpperLipX real",
                                                  "@attribute RightTopDipUpperLipY real",
                                                  "@attribute RightTopDipUpperLipZ real",
                                                  "@attribute TopLeftForeheadX real",                                                
                                                  "@attribute TopLeftForeheadY real",
                                                  "@attribute TopLeftForeheadZ real",
                                                  "@attribute MiddleTopLowerLipX real",                                                 
                                                  "@attribute MiddleTopLowerLipY real", 
                                                  "@attribute MiddleTopLowerLipZ real", 
                                                  "@attribute MiddleBottomLowerLipX real",
                                                  "@attribute MiddleBottomLowerLipY real",
                                                  "@attribute MiddleBottomLowerLipZ real",
                                                  "@attribute LeftOfLeftEyebrowX real",
                                                  "@attribute LeftOfLeftEyebrowY real",
                                                  "@attribute LeftOfLeftEyebrowZ real",
                                                  "@attribute MiddleTopOfLeftEyebrowX real",
                                                  "@attribute MiddleTopOfLeftEyebrowY real",
                                                  "@attribute MiddleTopOfLeftEyebrowZ real",
                                                  "@attribute RightOfLeftEyebrowX real",
                                                  "@attribute RightOfLeftEyebrowY real",
                                                  "@attribute RightOfLeftEyebrowZ real",
                                                  "@attribute MiddleBottomOfLeftEyebrowX real",
                                                  "@attribute MiddleBottomOfLeftEyebrowY real",
                                                  "@attribute MiddleBottomOfLeftEyebrowZ real",
                                                  "@attribute AboveMidUpperLeftEyelidX real",
                                                  "@attribute AboveMidUpperLeftEyelidY real",
                                                  "@attribute AboveMidUpperLeftEyelidZ real",
                                                  "@attribute OuterCornerOfLeftEyeX real",
                                                  "@attribute OuterCornerOfLeftEyeY real",
                                                  "@attribute OuterCornerOfLeftEyeZ real",
                                                  "@attribute MiddleTopLeftEyelidX real",
                                                  "@attribute MiddleTopLeftEyelidY real",
                                                  "@attribute MiddleTopLeftEyelidZ real",
                                                  "@attribute MiddleBottomLeftEyelidX real",
                                                  "@attribute MiddleBottomLeftEyelidY real",
                                                  "@attribute MiddleBottomLeftEyelidZ real",
                                                  "@attribute InnerCornerLeftEyeX real",
                                                  "@attribute InnerCornerLeftEyeY real",
                                                  "@attribute InnerCornerLeftEyeZ real",
                                                  "@attribute UnderMidBottomLeftEyelidX real",
                                                  "@attribute UnderMidBottomLeftEyelidY real",
                                                  "@attribute UnderMidBottomLeftEyelidZ real",
                                                  "@attribute LeftSideOfCheekX real",
                                                  "@attribute LeftSideOfCheekY real",
                                                  "@attribute LeftSideOfCheekZ real",
                                                  "@attribute OutsideLeftCornerMouthX real",
                                                  "@attribute OutsideLeftCornerMouthY real",
                                                  "@attribute OutsideLeftCornerMouthZ real",
                                                  "@attribute LeftOfChinX real",
                                                  "@attribute LeftOfChinY real",
                                                  "@attribute LeftOfChinZ real",
                                                  "@attribute LeftTopDipUpperLipX real",
                                                  "@attribute LeftTopDipUpperLipY real",
                                                  "@attribute LeftTopDipUpperLipZ real",
                                                  "@attribute OuterTopRightPupilX real",
                                                  "@attribute OuterTopRightPupilY real",
                                                  "@attribute OuterTopRightPupilZ real",
                                                  "@attribute OuterBottomRightPupilX real",
                                                  "@attribute OuterBottomRightPupilY real",
                                                  "@attribute OuterBottomRightPupilZ real",
                                                  "@attribute OuterTopLeftPupilX real",
                                                  "@attribute OuterTopLeftPupilY real",
                                                  "@attribute OuterTopLeftPupilZ real",
                                                  "@attribute OuterBottomLeftPupilX real",
                                                  "@attribute OuterBottomLeftPupilY real",
                                                  "@attribute OuterBottomLeftPupilZ real",
                                                  "@attribute InnerTopRightPupilX real",
                                                  "@attribute InnerTopRightPupilY real",
                                                  "@attribute InnerTopRightPupilZ real",
                                                  "@attribute InnerBottomRightPupilX real",
                                                  "@attribute InnerBottomRightPupilY real",
                                                  "@attribute InnerBottomRightPupilZ real",
                                                  "@attribute InnerTopLeftPupilX real",
                                                  "@attribute InnerTopLeftPupilY real",
                                                  "@attribute InnerTopLeftPupilZ real",
                                                  "@attribute InnerBottomLeftPupilX real",
                                                  "@attribute InnerBottomLeftPupilY real",
                                                  "@attribute InnerBottomLeftPupilZ real",
                                                  "@attribute RightTopUpperLipX real",
                                                  "@attribute RightTopUpperLipY real",
                                                  "@attribute RightTopUpperLipZ real",
                                                  "@attribute LeftTopUpperLipX real",
                                                  "@attribute LeftTopUpperLipY real",
                                                  "@attribute LeftTopUpperLipZ real",
                                                  "@attribute RightBottomUpperLipX real",
                                                  "@attribute RightBottomUpperLipY real",
                                                  "@attribute RightBottomUpperLipZ real",
                                                  "@attribute LeftBottomUpperLipX real",
                                                  "@attribute LeftBottomUpperLipY real",
                                                  "@attribute LeftBottomUpperLipZ real",
                                                  "@attribute RightTopLowerLipX real",
                                                  "@attribute RightTopLowerLipY real",
                                                  "@attribute RightTopLowerLipZ real",
                                                  "@attribute LeftTopLowerLipX real",
                                                  "@attribute LeftTopLowerLipY real",
                                                  "@attribute LeftTopLowerLipZ real",
                                                  "@attribute RightBottomLowerLipX real",
                                                  "@attribute RightBottomLowerLipY real",
                                                  "@attribute RightBottomLowerLipZ real",
                                                  "@attribute LeftBottomLowerLipX real",
                                                  "@attribute LeftBottomLowerLipY real",
                                                  "@attribute LeftBottomLowerLipZ real",
                                                  "@attribute MiddleBottomUpperLipX real",
                                                  "@attribute MiddleBottomUpperLipY real",
                                                  "@attribute MiddleBottomUpperLipZ real",
                                                  "@attribute LeftCornerMouthX real",
                                                  "@attribute LeftCornerMouthY real",
                                                  "@attribute LeftCornerMouthZ real",
                                                  "@attribute RightCornerMouthX real",
                                                  "@attribute RightCornerMouthY real",
                                                  "@attribute RightCornerMouthZ real",
                                                  "@attribute BottomOfRightCheekX real",
                                                  "@attribute BottomOfRightCheekY real",
                                                  "@attribute BottomOfRightCheekZ real",
                                                  "@attribute BottomOfLeftCheekX real",
                                                  "@attribute BottomOfLeftCheekY real",
                                                  "@attribute BottomOfLeftCheekZ real",
                                                  "@attribute AboveThreeFourthRightEyelidX real",
                                                  "@attribute AboveThreeFourthRightEyelidY real",
                                                  "@attribute AboveThreeFourthRightEyelidZ real",
                                                  "@attribute AboveThreeFourthLeftEyelidX real",
                                                  "@attribute AboveThreeFourthLeftEyelidY real",
                                                  "@attribute AboveThreeFourthLeftEyelidZ real",
                                                  "@attribute ThreeFourthTopRightEyelidX real",
                                                  "@attribute ThreeFourthTopRightEyelidY real",
                                                  "@attribute ThreeFourthTopRightEyelidZ real",
                                                  "@attribute ThreeFourthTopLeftEyelidX real",
                                                  "@attribute ThreeFourthTopLeftEyelidY real",
                                                  "@attribute ThreeFourthTopLeftEyelidZ real",
                                                  "@attribute ThreeFourthBottomRightEyelidX real",
                                                  "@attribute ThreeFourthBottomRightEyelidY real",
                                                  "@attribute ThreeFourthBottomRightEyelidZ real",
                                                  "@attribute ThreeFourthBottomLeftEyelidX real",
                                                  "@attribute ThreeFourthBottomLeftEyelidY real",
                                                  "@attribute ThreeFourthBottomLeftEyelidZ real",
                                                  "@attribute BelowThreeFourthRightEyelidX real",
                                                  "@attribute BelowThreeFourthRightEyelidY real",
                                                  "@attribute BelowThreeFourthRightEyelidZ real",
                                                  "@attribute BelowThreeFourthLeftEyelidX real",
                                                  "@attribute BelowThreeFourthLeftEyelidY real",
                                                  "@attribute BelowThreeFourthLeftEyelidZ real",
                                                  "@attribute AboveOneFourthRightEyelidX real",
                                                  "@attribute AboveOneFourthRightEyelidY real",
                                                  "@attribute AboveOneFourthRightEyelidZ real",
                                                  "@attribute AboveOneFourthLeftEyelidX real",
                                                  "@attribute AboveOneFourthLeftEyelidY real",
                                                  "@attribute AboveOneFourthLeftEyelidZ real",
                                                  "@attribute OneFourthTopRightEyelidX real",
                                                  "@attribute OneFourthTopRightEyelidY real",
                                                  "@attribute OneFourthTopRightEyelidZ real",
                                                  "@attribute OneFourthTopLeftEyelidX real",
                                                  "@attribute OneFourthTopLeftEyelidY real",
                                                  "@attribute OneFourthTopLeftEyelidZ real",
                                                  "@attribute OneFourthBottomRightEyelidX real",
                                                  "@attribute OneFourthBottomRightEyelidY real",
                                                  "@attribute OneFourthBottomRightEyelidZ real",
                                                  "@attribute OneFourthBottomLeftEyelidX real",
                                                  "@attribute OneFourthBottomLeftEyelidY real",
                                                  "@attribute OneFourthBottomLeftEyelidZ real",
                                                  "@attribute EmocionClasificada {Alegria, Enfado, Tristeza, Miedo, Asco, Sorpresa, Neutra }",
                                                  "@data" 
                                                 };

                System.IO.File.WriteAllLines(@"C:\Base De Datos\Script completo para Weka y Mattlab\BBDDEmocionesTopSkull.txt", lineaPrincipal);
                System.IO.File.WriteAllLines(@"C:\Base De Datos\Script completo para Weka y Mattlab\BBDDEmocionesNariz.txt", lineaPrincipal);

                string sumario = "";
                System.IO.File.WriteAllText(@"C:\Base De Datos\Script completo para Weka y Mattlab\Sumario.txt", sumario);

            }
        }
    }
}
