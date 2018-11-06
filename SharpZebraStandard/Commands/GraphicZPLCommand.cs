﻿using System.Drawing;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace SharpZebraStandard.Commands
{
    public partial class ZPLCommands
    {
        private static int _stringCounter;
        private static Printing.PrinterSettings _printerSettings;

        public class CustomString
        {
            private Font _font;
            private ElementDrawRotation _rotation;
            private string _text;

            public string Text
            {
                get => _text;
                set
                {
                    if (value == _text) return;
                    _text = value;
                    InitGraphic();
                }
            }

            public Font Font
            {
                get => _font;
                set
                {
                    if (Equals(value, _font)) return;
                    _font = value;
                    InitGraphic();
                }
            }

            public ElementDrawRotation Rotation
            {
                get => _rotation;
                set
                {
                    if (value == _rotation) return;
                    _rotation = value;
                    InitGraphic();                    
                }
            }

            public Bitmap CustomImage { get; private set; }

            public int TextWidth => CustomImage?.Width ?? 0;

            public int TextHeight => CustomImage?.Height ?? 0;

            private void InitGraphic()
            {
                if (_font == null || string.IsNullOrEmpty(_text))
                {
                    CustomImage = null;
                    return;
                }
                
                CustomImage = new Bitmap(1,1);
                var graphics = Graphics.FromImage(CustomImage);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                var sWidth = (int)graphics.MeasureString(_text, _font).Width;
                var sHeight = (int)graphics.MeasureString(_text, _font).Height;
                CustomImage = new Bitmap(CustomImage, sWidth, sHeight);

                using (var g = Graphics.FromImage(CustomImage))
                {                    
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    var stringFormat = new StringFormat()
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.None
                    };
                    
                    g.Clear(Color.White);
                    g.DrawString(_text, _font, new SolidBrush(Color.Black), 0, 0, stringFormat);
                    g.Flush();
                }                 
                switch (_rotation)
                {
                    case ElementDrawRotation.ROTATE_90_DEGREES:
                        CustomImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case ElementDrawRotation.ROTATE_180_DEGREES:
                        CustomImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        break;
                    case ElementDrawRotation.ROTATE_270_DEGREES:
                        CustomImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                }                
            }
        }

        /// <summary>
        /// Write any windows-supported text in any windows-supported font style to the printer - including international characters!
        /// Note that if your printer's RAM drive letter is something other than 'R', set the ramDrive variable or call ClearPrinter first!
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="rotation"></param>
        /// <param name="font"></param>
        /// <param name="text"></param>
        /// <param name="ramDrive">Location of your printer's ram drive</param>
        /// <returns>Array of bytes containing ZPLII data to be sent to the Zebra printer</returns>
        public static byte[] CustomStringWrite(int left, int top, ElementDrawRotation rotation, Font font, string text, char? ramDrive = null)
        {
            var s = new CustomString {Font = font, Rotation = rotation, Text = text};
            return CustomStringWrite(left, top, s, ramDrive);
        }

        public static byte[] CustomStringWrite(int left, int top, CustomString customString, char? ramDrive = null)
        {
            _stringCounter++;
            var name = $"SZT{_stringCounter:00000}";
            var res = new List<byte>();
            var drive = ramDrive ?? _printerSettings?.RamDrive ?? 'R';
            res.AddRange(GraphicStore(customString.CustomImage, drive, name));
            res.AddRange(GraphicWrite(left, top, name, drive));
            return res.ToArray();
        }

        public static byte[] GraphicWrite(int left, int top, string imageName, char storageArea)
        {
            return Encoding.GetEncoding(850).GetBytes($"^FO{left},{top}^XG{storageArea}:{imageName}.GRF^FS");
        }

        public static byte[] GraphicStore(Bitmap image, char storageArea, string imageName)
        {    
            //Note that we're using the RED channel to determine if each pixel of an image is enabled.  
            //No dithering is done: values of red higher than 128 are on.
            var res = new List<byte>();
            var byteWidth = image.Width % 8 == 0 ? image.Width / 8 : image.Width / 8 + 1;
            res.AddRange(Encoding.GetEncoding(850).GetBytes($"~DG{storageArea}:{imageName},{image.Height * byteWidth},{byteWidth},"));

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < byteWidth; x++)
                {
                    var ba = new BitArray(8);
                    var scanX = x * 8;
                    for (var k = 7; k >= 0; k--)
                    {
                        if (scanX >= image.Width)
                            ba[k] = false;
                        else
                            ba[k] = image.GetPixel(scanX, y).R < 128;
                        scanX++;
                    }
                    res.AddRange(Encoding.GetEncoding(850).GetBytes($"{ConvertToByte(ba):X2}"));                    
                }
                res.AddRange(Encoding.GetEncoding(850).GetBytes("\n"));
            }
            return res.ToArray(); 
        }

        public static byte[] GraphicDelete(char storageArea, string imageName)
        {
            return Encoding.GetEncoding(850).GetBytes($"^ID{storageArea}:{imageName}.GRF^FS");
        }
        
        private static byte ConvertToByte(BitArray bits)
        {
            byte value = 0x00;

            for (byte x = 0; x < 8; x++)
            {
                value |= (byte)(bits[x] ? 0x01 << x : 0x00);
            }
            return value;
        }       		 	
    }
}
