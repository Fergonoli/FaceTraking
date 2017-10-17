using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace FaceTrackingBasics
{
    public class PuntoNombrado
    {
        string NombrePunto = "";
        int IdentPunto = 0;
        Point3D Punto3D = new Point3D();

        public PuntoNombrado() { }


        public void ModificarIntegerData(int i) { IdentPunto = i; }
        public void ModificarStringData(string i) { NombrePunto = i; }
        public void ModificarPoint3DData(float X, float Y, float Z)
        {
            Punto3D.X = X;
            Punto3D.Y = Y;
            Punto3D.Z = Z;
        }

        public int DevolverInt() { return IdentPunto; }
        public string DevolverStr() { return NombrePunto; }
        public Point3D DevolverPunto() { return Punto3D; }

    }
}
