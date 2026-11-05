using System;
using System.IO;
namespace JingYue {
 public static class ImageDimensions {
  static int U16(BinaryReader r){return r.ReadByte()*256+r.ReadByte();}
  static int Big(byte[] b,int i){return (b[i]<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3];}
  public static void Read(Stream stream,ReadingImage image){try{using(var r=new BinaryReader(stream)){int a=r.ReadByte(),b=r.ReadByte();if(a==255&&b==216){int read=2;while(read<1024*1024){int marker;do{marker=r.ReadByte();read++;}while(marker!=255);do{marker=r.ReadByte();read++;}while(marker==255);if(marker==217||marker==218)break;if(marker==0||marker==216||marker>=208&&marker<=215)continue;int size=U16(r);read+=2;if(size<2)break;if((marker>=192&&marker<=195)||(marker>=197&&marker<=199)||(marker>=201&&marker<=203)||(marker>=205&&marker<=207)){r.ReadByte();image.PixelHeight=U16(r);image.PixelWidth=U16(r);return;}int skipped=r.ReadBytes(size-2).Length;read+=skipped;if(skipped!=size-2)break;}}
    else if(a==137&&b==80){var rest=r.ReadBytes(22);if(rest.Length==22){image.PixelWidth=Big(rest,14);image.PixelHeight=Big(rest,18);}}
    else if(a==71&&b==73){var rest=r.ReadBytes(8);if(rest.Length==8){image.PixelWidth=rest[4]|rest[5]<<8;image.PixelHeight=rest[6]|rest[7]<<8;}}
    else if(a==66&&b==77){var rest=r.ReadBytes(24);if(rest.Length==24){image.PixelWidth=BitConverter.ToInt32(rest,16);image.PixelHeight=Math.Abs(BitConverter.ToInt32(rest,20));}}
   }}catch{image.PixelWidth=0;image.PixelHeight=0;}if(image.PixelWidth<=0||image.PixelHeight<=0||image.PixelWidth>100000||image.PixelHeight>100000){image.PixelWidth=0;image.PixelHeight=0;}}
 }
}
