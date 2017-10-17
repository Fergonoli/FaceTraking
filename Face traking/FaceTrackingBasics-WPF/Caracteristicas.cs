using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace FaceTrackingBasics
{
    class Caracteristicas
    {
        double [] ValoresCaracteristica = new double[19];

        public Caracteristicas( List<PuntoNombrado> PuntosCara)
        {
   
	        //Informacion que se va a almacenar
	        double AngOjoIzq = 0;
	        double AngOjoDch = 0;
	        double DisCejasInterior = 0;
	        double DisOjoDchSupExt = 0;
	        double DisOjoIZqSupExt = 0;
	        double DisOjoDchSupInt = 0;
	        double DisOjoIZqSupInt = 0;
	        double DisOjoDchInfExt = 0;
	        double DisOjoIZqInfExt = 0;
	        double DisOjoDchInfInt = 0;
	        double DisOjoIZqInfInt = 0;
	        double DisOjoDchAlt = 0;
	        double DisOjoIzqAlt = 0;	
	        double DisLabioSupDch = 0;
	        double DisLabioSupIzq = 0;
	        double DisLabioInfDch = 0;
	        double DisLabioInfIzq = 0;	
	        double DisLabioLargo = 0;
	        double DisLabioAncho = 0;	
	
	
		    //Extraccion de los puntos necesarios
	
            //Coordenadas RightOfRightEyebrow
            double X15 = PuntosCara[5].DevolverPunto().X;
            double Y15 = PuntosCara[5].DevolverPunto().Y;
            double Z15 = PuntosCara[5].DevolverPunto().Z;
		
		    //Coordenadas LeftOfRightEyebrow
            double X17 = PuntosCara[7].DevolverPunto().X;
            double Y17 = PuntosCara[7].DevolverPunto().Y;
            double Z17 = PuntosCara[7].DevolverPunto().Z;
		
		    //Coordenadas InnerCornerRightEye
            double X23 = PuntosCara[13].DevolverPunto().X;
            double Y23 = PuntosCara[13].DevolverPunto().Y;
            double Z23 = PuntosCara[13].DevolverPunto().Z;
		
		    //Coordenadas InnerCornerLeftEye
            double X56 = PuntosCara[30].DevolverPunto().X;
            double Y56 = PuntosCara[30].DevolverPunto().Y;
            double Z56 = PuntosCara[30].DevolverPunto().Z;
		
		    //Coordenadas RightOfLeftEyebrow
            double X50 = PuntosCara[24].DevolverPunto().X;
            double Y50 = PuntosCara[24].DevolverPunto().Y;
            double Z50 = PuntosCara[24].DevolverPunto().Z;
		
		    //Coordenadas LeftOfLeftEyebrow
            double X48 = PuntosCara[22].DevolverPunto().X;
            double Y48 = PuntosCara[22].DevolverPunto().Y;
            double Z48 = PuntosCara[22].DevolverPunto().Z;
		
		    //Coordenadas OuterCornerOfRightEye
            double X20 = PuntosCara[10].DevolverPunto().X;
            double Y20 = PuntosCara[10].DevolverPunto().Y;
            double Z20 = PuntosCara[10].DevolverPunto().Z;
		
		    //Coordenadas MiddleTopRightEyelid
            double X21 = PuntosCara[11].DevolverPunto().X;
            double Y21 = PuntosCara[11].DevolverPunto().Y;
            double Z21 = PuntosCara[11].DevolverPunto().Z;
		
		    //Coordenadas MiddleTopLeftEyelid
            double X54 = PuntosCara[28].DevolverPunto().X;
            double Y54 = PuntosCara[28].DevolverPunto().Y;
            double Z54 = PuntosCara[28].DevolverPunto().Z;
		
		    //Coordenadas OuterCornerOfLeftEye
            double X53 = PuntosCara[27].DevolverPunto().X;
            double Y53 = PuntosCara[27].DevolverPunto().Y;
            double Z53 = PuntosCara[27].DevolverPunto().Z;
		
		    //Coordenadas MiddleBottomRightEyelid
            double X22 = PuntosCara[12].DevolverPunto().X;
            double Y22 = PuntosCara[12].DevolverPunto().Y;
            double Z22 = PuntosCara[12].DevolverPunto().Z;
		
		    //Coordenadas MiddleBottomLeftEyelid
            double X55 = PuntosCara[29].DevolverPunto().X;
            double Y55 = PuntosCara[29].DevolverPunto().Y;
            double Z55 = PuntosCara[29].DevolverPunto().Z;
		
		    //Coordenadas MiddleTopDipUpperLip
            double X7 = PuntosCara[2].DevolverPunto().X;
            double Y7 = PuntosCara[2].DevolverPunto().Y;
            double Z7 = PuntosCara[2].DevolverPunto().Z;

		    //Coordenadas RightCornerMouth
            double X89 = PuntosCara[54].DevolverPunto().X;
            double Y89 = PuntosCara[54].DevolverPunto().Y;
            double Z89 = PuntosCara[54].DevolverPunto().Z;
		
		    //Coordenadas LeftCornerMouth
            double X88 = PuntosCara[53].DevolverPunto().X;
            double Y88 = PuntosCara[53].DevolverPunto().Y;
            double Z88 = PuntosCara[53].DevolverPunto().Z;
		
            //Coordenadas MiddleBottomLowerLip
            double X41 = PuntosCara[21].DevolverPunto().X;
            double Y41 = PuntosCara[21].DevolverPunto().Y;
            double Z41 = PuntosCara[21].DevolverPunto().Z;
		
		    //Calcular AngOjoIzq 		
		    //VectorCejaDch entre puntos 15 y 17
		    double  VectorCejaDchX = X15 - X17;
		    double  VectorCejaDchY = Y15 - Y17;
		    double  VectorCejaDchZ = Z15 - Z17;
		
		    //VectorMedioOjos entre puntos 23 y 56
		    double  VectorMedioOjosX = X23 - X56;
		    double  VectorMedioOjosY = Y23 - Y56;
		    double  VectorMedioOjosZ = Z23 - Z56;		
		
		    double  coseno = ( Math.Abs( VectorCejaDchX*VectorMedioOjosX + 
                                         VectorCejaDchY*VectorMedioOjosY + 
                                         VectorCejaDchZ*VectorMedioOjosZ )
                                         / 
                                         ( Math.Sqrt( Math.Pow(VectorCejaDchX,2) + 
                                                      Math.Pow(VectorCejaDchY,2) +
                                                      Math.Pow(VectorCejaDchZ,2) )
                                                      *
                                           Math.Sqrt( Math.Pow(VectorMedioOjosX, 2) + 
                                                      Math.Pow(VectorMedioOjosY,2) + 
                                                      Math.Pow(VectorMedioOjosZ,2))));
		    AngOjoIzq = (double) Math.Acos(coseno);
		
            //Almacenamos 1º caracteristica
            this.ValoresCaracteristica[0] = AngOjoIzq;
		
		    //Calcular AngOjoDch	
		    //VectorCejaDch entre puntos 50 y 48
		    double  VectorCejaIzqX = X50 - X17;
		    double  VectorCejaIzqY = Y50 - Y17;
		    double  VectorCejaIzqZ = Z50 - Z17;
				
		    double  coseno2 = ( Math.Abs( VectorCejaIzqX*VectorMedioOjosX + 
                                         VectorCejaIzqY*VectorMedioOjosY + 
                                         VectorCejaIzqZ*VectorMedioOjosZ )
                                         / 
                                         ( Math.Sqrt( Math.Pow(VectorCejaIzqX,2) +
                                                      Math.Pow(VectorCejaIzqY,2) + 
                                                      Math.Pow(VectorCejaIzqZ,2) ) 
                                                      * 
                                           Math.Sqrt( Math.Pow(VectorMedioOjosX,2) +
                                                      Math.Pow(VectorMedioOjosY,2) + 
                                                      Math.Pow(VectorMedioOjosZ,2))));
		    AngOjoDch =  (double) Math.Acos(coseno2);

            //Almacenamos 2º caracteristica
            this.ValoresCaracteristica[1] = AngOjoDch;			


		    //Calcular DisCejasInterior entre puntos 17 y 50
		    double VDisCejasInteriorX = X17 - X50;
		    double VDisCejasInteriorY = Y17 - Y50;
		    double VDisCejasInteriorZ = Z17 - Z50;

		    DisCejasInterior = Math.Sqrt( Math.Pow(VDisCejasInteriorX,2) +
                                          Math.Pow(VDisCejasInteriorY,2) +
                                          Math.Pow(VDisCejasInteriorZ,2));		

            //Almacenamos 3º caracteristica
            this.ValoresCaracteristica[2] = DisCejasInterior;		


		    //Calcular DisOjoDchSupExt entre puntos 20 y 21
		    double VDisOjoDchSupExtX = X20 - X21;
		    double VDisOjoDchSupExtY = Y20 - Y21;
		    double VDisOjoDchSupExtZ = Z20 - Z21;
		        
            DisOjoDchSupExt = Math.Sqrt( Math.Pow(VDisOjoDchSupExtX,2) +
                                          Math.Pow(VDisOjoDchSupExtY,2) +
                                          Math.Pow(VDisOjoDchSupExtZ,2));
		                     
            //Almacenamos 4º caracteristica
            this.ValoresCaracteristica[3] = DisOjoDchSupExt;		


		    //Calcular DisOjoIZqSupExt entre puntos 54 y 53
		    double VDisOjoIZqSupExtX = X54 - X53;
		    double VDisOjoIZqSupExtY = Y54 - Y53;
		    double VDisOjoIZqSupExtZ = Z54 - Z53;
		    DisOjoIZqSupExt = Math.Sqrt( Math.Pow(VDisOjoIZqSupExtX,2) +
                                          Math.Pow(VDisOjoIZqSupExtY,2) +
                                          Math.Pow(VDisOjoIZqSupExtZ,2));
		                     
            //Almacenamos 5º caracteristica
            this.ValoresCaracteristica[4] = DisOjoIZqSupExt;		
		

		    //Calcular DisOjoDchSupInt entre puntos 21 y 23
		    double VDisOjoDchSupIntX = X21 - X23;
		    double VDisOjoDchSupIntY = Y21 - Y23;
		    double VDisOjoDchSupIntZ = Z21 - Z23;
		    DisOjoDchSupInt = Math.Sqrt( Math.Pow(VDisOjoDchSupIntX,2) +
                                          Math.Pow(VDisOjoDchSupIntY,2) +
                                          Math.Pow(VDisOjoDchSupIntZ,2));
		                     
            //Almacenamos 6º caracteristica
            this.ValoresCaracteristica[5] = DisOjoDchSupInt;		
		
		
		    //Calcular DisOjoIZqSupInt entre puntos 56 y 54
		    double VDisOjoIZqSupIntX = X56 - X54;
		    double VDisOjoIZqSupIntY = Y56 - Y54;
		    double VDisOjoIZqSupIntZ = Z56 - Z54;
		    DisOjoIZqSupInt = Math.Sqrt( Math.Pow(VDisOjoIZqSupIntX,2) +
                                          Math.Pow(VDisOjoIZqSupIntY,2) +
                                          Math.Pow(VDisOjoIZqSupIntZ,2));
		                     
            //Almacenamos 7º caracteristica
            this.ValoresCaracteristica[6] = DisOjoIZqSupInt;		
		
	
		    //Calcular DisOjoDchInfExt entre puntos 20 y 22
		    double VDisOjoDchInfExtX = X20 - X22;
		    double VDisOjoDchInfExtY = Y20 - Y22;
		    double VDisOjoDchInfExtZ = Z20 - Z22;
		    DisOjoDchInfExt =  Math.Sqrt( Math.Pow(VDisOjoDchInfExtX,2) +
                                          Math.Pow(VDisOjoDchInfExtY,2) +
                                          Math.Pow(VDisOjoDchInfExtZ,2));
		                     
            //Almacenamos 8º caracteristica
            this.ValoresCaracteristica[7] = DisOjoDchInfExt;	
		

		    //Calcular DisOjoIZqInfExt entre puntos 55 y 53
		    double VDisOjoIZqInfExtX = X55 - X53;
		    double VDisOjoIZqInfExtY = Y55 - Y53;
		    double VDisOjoIZqInfExtZ = Z55 - Z53;
		    DisOjoIZqInfExt =  Math.Sqrt( Math.Pow(VDisOjoIZqInfExtX,2) +
                                          Math.Pow(VDisOjoIZqInfExtY,2) +
                                          Math.Pow(VDisOjoIZqInfExtZ,2));
		                     
            //Almacenamos 9º caracteristica
            this.ValoresCaracteristica[8] = DisOjoIZqInfExt;
		
		    
            //Calcular DisOjoDchInfInt entre puntos 22 y 23
		    double VDisOjoDchInfIntX = X22 - X23;
		    double VDisOjoDchInfIntY = Y22 - Y23;
		    double VDisOjoDchInfIntZ = Z22 - Z23;
		    DisOjoDchInfInt =  Math.Sqrt( Math.Pow(VDisOjoDchInfIntX,2) +
                                          Math.Pow(VDisOjoDchInfIntY,2) +
                                          Math.Pow(VDisOjoDchInfIntZ,2));
		                     
            //Almacenamos 10º caracteristica
            this.ValoresCaracteristica[9] = DisOjoDchInfInt;
		
			
		    //Calcular DisOjoIZqInfInt entre puntos 56 y 55
		    double VDisOjoIZqInfIntX = X56 - X55;
		    double VDisOjoIZqInfIntY = Y56 - Y55;
		    double VDisOjoIZqInfIntZ = Z56 - Z55;
		    DisOjoIZqInfInt =   Math.Sqrt( Math.Pow(VDisOjoIZqInfIntX,2) +
                                          Math.Pow(VDisOjoIZqInfIntY,2) +
                                          Math.Pow(VDisOjoIZqInfIntZ,2));
		                     
            //Almacenamos 11º caracteristica
            this.ValoresCaracteristica[10] = DisOjoIZqInfInt;
		
		    
            //Calcular DisOjoDchAlt entre puntos 21 y 22
		    double VDisOjoDchAltX = X21 - X22;
		    double VDisOjoDchAltY = Y21 - Y22;
		    double VDisOjoDchAltZ = Z21 - Z22;
		    DisOjoDchAlt =  Math.Sqrt( Math.Pow(VDisOjoDchAltX,2) +
                                          Math.Pow(VDisOjoDchAltY,2) +
                                          Math.Pow(VDisOjoDchAltZ,2));
		                     
            //Almacenamos 12º caracteristica
            this.ValoresCaracteristica[11] = DisOjoDchAlt;
		
		        
            //Calcular DisOjoIzqAlt entre puntos 54 y 55
		    double VDisOjoIzqAltX = X54 - X55;
		    double VDisOjoIzqAltY = Y54 - Y55;
		    double VDisOjoIzqAltZ = Z54 - Z55;
		    DisOjoIzqAlt =  Math.Sqrt( Math.Pow(VDisOjoIzqAltX,2) +
                                          Math.Pow(VDisOjoIzqAltY,2) +
                                          Math.Pow(VDisOjoIzqAltZ,2));
		                     
            //Almacenamos 13º caracteristica
            this.ValoresCaracteristica[12] = DisOjoIzqAlt;
		
		        
            //Calcular DisLabioSupDch entre puntos 89 y 7
		    double VDisLabioSupDchX = X89 - X7;
		    double VDisLabioSupDchY = Y89 - Y7;
		    double VDisLabioSupDchZ = Z89 - Z7;
		    DisLabioSupDch =  Math.Sqrt( Math.Pow(VDisLabioSupDchX,2) +
                                          Math.Pow(VDisLabioSupDchY,2) +
                                          Math.Pow(VDisLabioSupDchZ,2));
		                     
            //Almacenamos 14º caracteristica
            this.ValoresCaracteristica[13] = DisLabioSupDch;
		
		        
            //Calcular DisLabioSupIzq entre puntos 7 y 88
		    double VDisLabioSupIzqX = X7 - X88;
		    double VDisLabioSupIzqY = Y7 - Y88;
		    double VDisLabioSupIzqZ = Z7 - Z88;
		    DisLabioSupIzq =  Math.Sqrt( Math.Pow(VDisLabioSupIzqX,2) +
                                          Math.Pow(VDisLabioSupIzqY,2) +
                                          Math.Pow(VDisLabioSupIzqZ,2));
		                     
            //Almacenamos 15º caracteristica
            this.ValoresCaracteristica[14] = DisLabioSupIzq;
		
		        
            //Calcular DisLabioInfDch entre puntos 89 y 41
		    double VDisLabioInfDchX = X89 - X41;
		    double VDisLabioInfDchY = Y89 - Y41;
		    double VDisLabioInfDchZ = Z89 - Z41;
		    DisLabioInfDch =  Math.Sqrt( Math.Pow(VDisLabioInfDchX,2) +
                                          Math.Pow(VDisLabioInfDchY,2) +
                                          Math.Pow(VDisLabioInfDchZ,2));
		                     
            //Almacenamos 16º caracteristica
            this.ValoresCaracteristica[15] = DisLabioInfDch;
		
		
		        
            //Calcular DisLabioInfIzq entre puntos 41 y 88
		    double VDisLabioInfIzqX = X41 - X88;
		    double VDisLabioInfIzqY = Y41 - Y88;
		    double VDisLabioInfIzqZ = Z41 - Z88;
		    DisLabioInfIzq =  Math.Sqrt( Math.Pow(VDisLabioInfIzqX,2) +
                                          Math.Pow(VDisLabioInfIzqY,2) +
                                          Math.Pow(VDisLabioInfIzqZ,2));
		                     
            //Almacenamos 17º caracteristica
            this.ValoresCaracteristica[16] = DisLabioInfIzq;
		
		    
            //Calcular DisLabioLargo entre puntos 89 y 88
		    double VDisLabioLargoX = X89 - X88;
		    double VDisLabioLargoY = Y89 - Y88;
		    double VDisLabioLargoZ = Z89 - Z88;
		    DisLabioLargo = Math.Sqrt( Math.Pow(VDisLabioLargoX,2) +
                                          Math.Pow(VDisLabioLargoY,2) +
                                          Math.Pow(VDisLabioLargoZ,2));
		                     
            //Almacenamos 18º caracteristica
            this.ValoresCaracteristica[17] = DisLabioLargo;
		
		    
            //Calcular DisLabioAncho entre puntos 7 y 41
		    double VDisLabioAnchoX = X7 - X41;
		    double VDisLabioAnchoY = Y7 - Y41;
		    double VDisLabioAnchoZ = Z7 - Z41;
		    DisLabioAncho =  Math.Sqrt( Math.Pow(VDisLabioAnchoX,2) +
                                          Math.Pow(VDisLabioAnchoY,2) +
                                          Math.Pow(VDisLabioAnchoZ,2));
		                     
            //Almacenamos 19º caracteristica
            this.ValoresCaracteristica[18] = DisLabioAncho;
        }

        public double[] DevolverCaracteristicas()
        {
            return this.ValoresCaracteristica;
        }
    }
}
