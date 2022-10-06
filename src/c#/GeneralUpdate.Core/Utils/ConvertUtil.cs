using System.Text;

namespace GeneralUpdate.Core.Utils
{
    public class ConvertUtil
    {
        public static Encoding ToEncoding(int type)
        {
            Encoding encoding = Encoding.Default;
            switch (type)
            {
                case 1:
                    encoding = Encoding.UTF8;
                    break;

                case 2:
                    encoding = Encoding.UTF7;
                    break;

                case 3:
                    encoding = Encoding.UTF32;
                    break;

                case 4:
                    encoding = Encoding.Unicode;
                    break;

                case 5:
                    encoding = Encoding.BigEndianUnicode;
                    break;

                case 6:
                    encoding = Encoding.ASCII;
                    break;

                case 7:
                    encoding = Encoding.Default;
                    break;
            }
            return encoding;
        }

        public static int ToEncodingType(Encoding encoding)
        {
            int type = -1;
            if (encoding == Encoding.UTF8)
            {
                type = 1;
            }
            else if (encoding == Encoding.UTF7)
            {
                type = 2;
            }
            else if (encoding == Encoding.UTF32)
            {
                type = 3;
            }
            else if (encoding == Encoding.Unicode)
            {
                type = 4;
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                type = 5;
            }
            else if (encoding == Encoding.ASCII)
            {
                type = 6;
            }
            else if (encoding == Encoding.Default)
            {
                type = 7;
            }
            return type;
        }
    }
}