using System;

namespace InkSharp
{
    /// <summary>
    /// Imagen en el flujo del documento. Con solo ancho o solo alto se conserva la proporción;
    /// con ambos, se ajusta dentro del recuadro sin deformarse.
    /// </summary>
    public sealed class ImageBlock : Element
    {
        private readonly PdfImage _image;
        private double? _width;
        private double? _height;
        private TextAlign? _align;

        internal ImageBlock(PdfImage image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public ImageBlock Width(double width)
        {
            _width = Positive(width, nameof(width));
            return this;
        }

        public ImageBlock Height(double height)
        {
            _height = Positive(height, nameof(height));
            return this;
        }

        public ImageBlock AlignLeft() => Align(TextAlign.Left);

        public ImageBlock AlignCenter() => Align(TextAlign.Center);

        public ImageBlock AlignRight() => Align(TextAlign.Right);

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
        {
            double ratio = (double)_image.Height / _image.Width;
            double w, h;
            if (_width.HasValue && _height.HasValue)
            {
                double scale = Math.Min(_width.Value / _image.Width, _height.Value / _image.Height);
                w = _image.Width * scale;
                h = _image.Height * scale;
            }
            else
            {
                _image.Scale(_width, _height, out w, out h);
            }

            if (w > width)
            {
                w = width;
                h = w * ratio;
            }

            if (h > height + 1e-6)
            {
                if (!force)
                    return LayoutResult.DoesNotFit(this);
                // Más alta que una página vacía: se reduce para que quepa.
                h = height;
                w = h / ratio;
            }

            double offset = (_align ?? style.Align ?? TextAlign.Left) switch
            {
                TextAlign.Center => (width - w) / 2,
                TextAlign.Right => width - w,
                _ => 0,
            };

            return LayoutResult.Done(new Fragment(h, (context, x, y) => context.Page.DrawImage(_image, x + offset, y, w, h)));
        }

        private ImageBlock Align(TextAlign align)
        {
            _align = align;
            return this;
        }

        private static double Positive(double value, string name)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(name, "La medida debe ser positiva.");
            return value;
        }
    }

    /// <summary>Línea horizontal a todo el ancho.</summary>
    public sealed class DividerBlock : Element
    {
        private PdfColor _color = PdfColor.FromGray(0.75);
        private double _thickness = 1;

        internal DividerBlock()
        {
        }

        public DividerBlock Color(PdfColor color)
        {
            _color = color;
            return this;
        }

        public DividerBlock Color(string hex) => Color(PdfColor.FromHex(hex));

        public DividerBlock Thickness(double thickness)
        {
            if (thickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(thickness), "El grosor debe ser positivo.");
            _thickness = thickness;
            return this;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
        {
            if (_thickness > height && !force)
                return LayoutResult.DoesNotFit(this);

            double thickness = _thickness;
            PdfColor color = _color;
            return LayoutResult.Done(new Fragment(thickness, (context, x, y) =>
                context.Page.DrawLine(x, y + thickness / 2, x + width, y + thickness / 2, color, thickness)));
        }
    }
}
