using System;
using System.Collections.Generic;
using System.Text.Json;
using static ClientApp.CustomTemplateDesignerWindow;

namespace ClientApp.Services
{
    public static class DefaultTemplateService
    {
        public static List<DesignerBlock> GetTemplateBlocks(string id)
        {
            bool h = id.Contains("Half");
            double pH = h ? 561 : 1123, pW = 794, m = 40, cW = pW - 2 * m;
            var B = new List<DesignerBlock>();
            void A(DesignerBlock b) { b.IsHalfA4 = h; B.Add(b); }

            if (id.Contains("Corporate")) BuildCorporate(A, h, pH, pW, m, cW);
            else if (id.Contains("Elegant")) BuildElegant(A, h, pH, pW, m, cW);
            else if (id.Contains("ModernDark")) BuildModern(A, h, pH, pW, m, cW);
            else BuildTechnical(A, h, pH, pW, m, cW);
            return B;
        }

        static void BuildCorporate(Action<DesignerBlock> A, bool h, double pH, double pW, double m, double cW)
        {
            double y=0, lc=m, rc=pW/2+5, lw=pW/2-m-5, rw=pW/2-m-5;
            string bdr="#1565C0", lbl="#888888", txt="#111111";
            // Header
            A(new DesignerBlock{Id="rect",X=0,Y=0,Width=pW,Height=50,ColorHex=bdr});
            A(new DesignerBlock{Id="custom_text",CustomText="SERVICE JOB ORDER",X=m,Y=12,Width=300,Height=26,FontSize=16,IsBold=true,ColorHex="#FFFFFF"});
            A(new DesignerBlock{Id="memo_id",X=pW-m-200,Y=14,Width=200,Height=24,FontSize=14,IsBold=true,ColorHex="#FFFFFF",TextAlignment="Right"});
            y=60;
            // Company row
            A(new DesignerBlock{Id="name",X=m,Y=y,Width=cW/2,Height=28,FontSize=16,IsBold=true,ColorHex=bdr});
            A(new DesignerBlock{Id="date",X=pW/2,Y=y,Width=cW/2,Height=24,FontSize=11,ColorHex="#666",TextAlignment="Right"});
            y+=28;
            A(new DesignerBlock{Id="address",X=m,Y=y,Width=cW/2,Height=18,FontSize=9,ColorHex="#666"});
            A(new DesignerBlock{Id="phone",X=m,Y=y+16,Width=cW/2,Height=18,FontSize=9,ColorHex="#666"});
            y+=42;
            // Table header
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=22,ColorHex="#E3F2FD"});
            A(new DesignerBlock{Id="custom_text",CustomText="FIELD",X=m+6,Y=y+3,Width=140,Height=18,FontSize=8,IsBold=true,ColorHex=bdr});
            A(new DesignerBlock{Id="custom_text",CustomText="DETAILS",X=m+150,Y=y+3,Width=200,Height=18,FontSize=8,IsBold=true,ColorHex=bdr});
            y+=22;
            // Table rows
            string[] labels={"Customer Name","Phone","Device / Model","Brand","Serial No.","Accessories","Estimated Cost"};
            string[] fields={"customer_name","customer_phone","model","brand","serial_number","accessories","cost"};
            for(int i=0;i<labels.Length;i++){
                string bg=i%2==0?"#F8F9FA":"#FFFFFF";
                A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=24,ColorHex=bg});
                A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#DEE2E6"});
                A(new DesignerBlock{Id="custom_text",CustomText=labels[i],X=m+6,Y=y+4,Width=140,Height=18,FontSize=9,IsBold=true,ColorHex=lbl});
                A(new DesignerBlock{Id=fields[i],X=m+150,Y=y+4,Width=cW-160,Height=18,FontSize=10,ColorHex=txt});
                y+=24;
            }
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#DEE2E6"});
            y+=14;
            // Issue
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=22,ColorHex="#E3F2FD"});
            A(new DesignerBlock{Id="custom_text",CustomText="COMPLAINT / ISSUE",X=m+6,Y=y+3,Width=300,Height=18,FontSize=8,IsBold=true,ColorHex=bdr});
            y+=22;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=bdr});
            double iH=h?60:160;
            A(new DesignerBlock{Id="issue",X=m+6,Y=y+6,Width=cW-12,Height=iH,FontSize=10,ColorHex=txt});
            y+=iH+14;
            // Diagnostics
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=22,ColorHex="#E3F2FD"});
            A(new DesignerBlock{Id="custom_text",CustomText="DIAGNOSTICS / NOTES",X=m+6,Y=y+3,Width=300,Height=18,FontSize=8,IsBold=true,ColorHex=bdr});
            y+=22;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=bdr});
            A(new DesignerBlock{Id="diagnostics",X=m+6,Y=y+6,Width=cW-12,Height=h?40:90,FontSize=10,ColorHex="#333",IsItalic=true});
            // Footer
            double tY=pH-(h?85:130);
            A(new DesignerBlock{Id="line",X=m,Y=tY,Width=cW,Height=1,ColorHex="#DEE2E6"});
            A(new DesignerBlock{Id="custom_text",CustomText="Terms & Conditions",X=m,Y=tY+4,Width=cW,Height=14,FontSize=7,IsBold=true,ColorHex="#AAA"});
            A(new DesignerBlock{Id="terms",X=m,Y=tY+18,Width=cW,Height=h?22:40,FontSize=6,ColorHex="#AAA"});
            double sY=pH-42;
            A(new DesignerBlock{Id="line",X=m,Y=sY,Width=200,Height=1,ColorHex=bdr});
            A(new DesignerBlock{Id="custom_text",CustomText="Customer Signature",X=m,Y=sY+3,Width=200,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
            A(new DesignerBlock{Id="line",X=pW-m-200,Y=sY,Width=200,Height=1,ColorHex=bdr});
            A(new DesignerBlock{Id="custom_text",CustomText="Authorized Signature",X=pW-m-200,Y=sY+3,Width=200,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
        }

        static void BuildElegant(Action<DesignerBlock> A, bool h, double pH, double pW, double m, double cW)
        {
            double y=h?20:35;
            string ac="#2E7D32", lbl="#777", txt="#111";
            A(new DesignerBlock{Id="name",X=m,Y=y,Width=cW,Height=32,FontSize=22,IsBold=true,ColorHex=ac,TextAlignment="Center"});
            y+=32;
            A(new DesignerBlock{Id="address",X=m,Y=y,Width=cW,Height=16,FontSize=9,ColorHex="#888",TextAlignment="Center"});
            y+=15;
            A(new DesignerBlock{Id="phone",X=m,Y=y,Width=cW,Height=16,FontSize=9,ColorHex="#888",TextAlignment="Center"});
            y+=24;
            A(new DesignerBlock{Id="line",X=pW/2-100,Y=y,Width=200,Height=2,ColorHex=ac});
            y+=10;
            A(new DesignerBlock{Id="memo_id",X=m,Y=y,Width=cW/2,Height=22,FontSize=13,IsBold=true,ColorHex=txt});
            A(new DesignerBlock{Id="date",X=pW/2,Y=y,Width=cW/2,Height=22,FontSize=11,ColorHex="#666",TextAlignment="Right"});
            y+=30;
            // Two-column table
            double hw=(cW-10)/2;
            // Left: Customer
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=hw,Height=20,ColorHex="#E8F5E9"});
            A(new DesignerBlock{Id="custom_text",CustomText="CUSTOMER DETAILS",X=m+6,Y=y+3,Width=hw-12,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            A(new DesignerBlock{Id="rect",X=m+hw+10,Y=y,Width=hw,Height=20,ColorHex="#E8F5E9"});
            A(new DesignerBlock{Id="custom_text",CustomText="DEVICE DETAILS",X=m+hw+16,Y=y+3,Width=hw-12,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            y+=20;
            // Customer rows
            string[][] leftRows={new[]{"Name","customer_name"},new[]{"Phone","customer_phone"}};
            string[][] rightRows={new[]{"Model","model"},new[]{"Brand","brand"},new[]{"Serial","serial_number"},new[]{"Accessories","accessories"}};
            double ly=y;
            foreach(var r in leftRows){
                A(new DesignerBlock{Id="line",X=m,Y=ly,Width=hw,Height=1,ColorHex="#C8E6C9"});
                A(new DesignerBlock{Id="custom_text",CustomText=r[0],X=m+6,Y=ly+3,Width=80,Height=18,FontSize=8,IsBold=true,ColorHex=lbl});
                A(new DesignerBlock{Id=r[1],X=m+90,Y=ly+3,Width=hw-96,Height=18,FontSize=10,ColorHex=txt});
                ly+=22;
            }
            double ry=y;
            foreach(var r in rightRows){
                A(new DesignerBlock{Id="line",X=m+hw+10,Y=ry,Width=hw,Height=1,ColorHex="#C8E6C9"});
                A(new DesignerBlock{Id="custom_text",CustomText=r[0],X=m+hw+16,Y=ry+3,Width=80,Height=18,FontSize=8,IsBold=true,ColorHex=lbl});
                A(new DesignerBlock{Id=r[1],X=m+hw+100,Y=ry+3,Width=hw-106,Height=18,FontSize=10,ColorHex=txt});
                ry+=22;
            }
            y=Math.Max(ly,ry)+8;
            // Cost
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=24,ColorHex="#E8F5E9"});
            A(new DesignerBlock{Id="custom_text",CustomText="ESTIMATED COST",X=m+6,Y=y+4,Width=140,Height=18,FontSize=9,IsBold=true,ColorHex=ac});
            A(new DesignerBlock{Id="cost",X=m+150,Y=y+4,Width=cW-160,Height=18,FontSize=11,IsBold=true,ColorHex=txt});
            y+=32;
            // Complaint
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=20,ColorHex="#E8F5E9"});
            A(new DesignerBlock{Id="custom_text",CustomText="COMPLAINT",X=m+6,Y=y+3,Width=200,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            y+=20;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="issue",X=m+6,Y=y+5,Width=cW-12,Height=h?50:140,FontSize=10,ColorHex=txt});
            y+=(h?60:150);
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=20,ColorHex="#E8F5E9"});
            A(new DesignerBlock{Id="custom_text",CustomText="DIAGNOSTICS",X=m+6,Y=y+3,Width=200,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            y+=20;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="diagnostics",X=m+6,Y=y+5,Width=cW-12,Height=h?30:80,FontSize=10,ColorHex="#333",IsItalic=true});
            double tY=pH-(h?80:120);
            A(new DesignerBlock{Id="line",X=m,Y=tY,Width=cW,Height=1,ColorHex="#C8E6C9"});
            A(new DesignerBlock{Id="terms",X=m,Y=tY+4,Width=cW,Height=h?22:40,FontSize=6,ColorHex="#AAA"});
            double sY=pH-38;
            A(new DesignerBlock{Id="line",X=m,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Customer Signature",X=m,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
            A(new DesignerBlock{Id="line",X=pW-m-190,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Authorized Signature",X=pW-m-190,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
        }

        static void BuildModern(Action<DesignerBlock> A, bool h, double pH, double pW, double m, double cW)
        {
            double y=0;
            string ac="#D84315", lbl="#888", txt="#111";
            // Thin top accent
            A(new DesignerBlock{Id="rect",X=0,Y=0,Width=pW,Height=5,ColorHex=ac});
            y=18;
            A(new DesignerBlock{Id="name",X=m,Y=y,Width=cW*0.6,Height=28,FontSize=18,IsBold=true,ColorHex="#222"});
            A(new DesignerBlock{Id="memo_id",X=pW/2,Y=y,Width=cW/2,Height=28,FontSize=15,IsBold=true,ColorHex=ac,TextAlignment="Right"});
            y+=28;
            A(new DesignerBlock{Id="address",X=m,Y=y,Width=cW*0.6,Height=16,FontSize=8,ColorHex="#888"});
            A(new DesignerBlock{Id="date",X=pW/2,Y=y,Width=cW/2,Height=16,FontSize=10,ColorHex="#666",TextAlignment="Right"});
            y+=16;
            A(new DesignerBlock{Id="phone",X=m,Y=y,Width=cW*0.6,Height=16,FontSize=8,ColorHex="#888"});
            y+=24;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=2,ColorHex=ac});
            y+=12;
            // Grid: 3 columns
            double col=cW/3;
            string[] lb={"Customer","Phone","Device","Brand","Serial","Accessories"};
            string[] fd={"customer_name","customer_phone","model","brand","serial_number","accessories"};
            for(int i=0;i<6;i+=3){
                int end=Math.Min(i+3,6);
                for(int j=i;j<end;j++){
                    double cx=m+(j-i)*col;
                    A(new DesignerBlock{Id="custom_text",CustomText=lb[j],X=cx+4,Y=y,Width=col-8,Height=14,FontSize=7,IsBold=true,ColorHex=lbl});
                    A(new DesignerBlock{Id=fd[j],X=cx+4,Y=y+14,Width=col-8,Height=20,FontSize=10,IsBold=j%3==0,ColorHex=txt});
                }
                y+=38;
            }
            // Cost row
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=26,ColorHex="#FBE9E7"});
            A(new DesignerBlock{Id="custom_text",CustomText="ESTIMATED COST",X=m+6,Y=y+5,Width=150,Height=18,FontSize=9,IsBold=true,ColorHex=ac});
            A(new DesignerBlock{Id="cost",X=m+160,Y=y+5,Width=cW-170,Height=18,FontSize=12,IsBold=true,ColorHex=txt});
            y+=34;
            A(new DesignerBlock{Id="custom_text",CustomText="COMPLAINT",X=m,Y=y,Width=cW,Height=16,FontSize=9,IsBold=true,ColorHex=ac});
            y+=18;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#FFCCBC"});
            A(new DesignerBlock{Id="issue",X=m+4,Y=y+5,Width=cW-8,Height=h?55:150,FontSize=10,ColorHex=txt});
            y+=(h?65:160);
            A(new DesignerBlock{Id="custom_text",CustomText="DIAGNOSTICS",X=m,Y=y,Width=cW,Height=16,FontSize=9,IsBold=true,ColorHex=ac});
            y+=18;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#FFCCBC"});
            A(new DesignerBlock{Id="diagnostics",X=m+4,Y=y+5,Width=cW-8,Height=h?35:90,FontSize=10,ColorHex="#333",IsItalic=true});
            double tY=pH-(h?80:120);
            A(new DesignerBlock{Id="line",X=m,Y=tY,Width=cW,Height=1,ColorHex="#EEE"});
            A(new DesignerBlock{Id="terms",X=m,Y=tY+4,Width=cW,Height=h?22:40,FontSize=6,ColorHex="#AAA"});
            double sY=pH-38;
            A(new DesignerBlock{Id="line",X=m,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Customer Signature",X=m,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
            A(new DesignerBlock{Id="line",X=pW-m-190,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Authorized Signature",X=pW-m-190,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
        }

        static void BuildTechnical(Action<DesignerBlock> A, bool h, double pH, double pW, double m, double cW)
        {
            double y=0;
            string ac="#37474F", lbl="#78909C", txt="#111";
            A(new DesignerBlock{Id="rect",X=0,Y=0,Width=pW,Height=6,ColorHex=ac});
            y=16;
            A(new DesignerBlock{Id="name",X=m,Y=y,Width=cW*0.55,Height=26,FontSize=17,IsBold=true,ColorHex=ac});
            A(new DesignerBlock{Id="memo_id",X=pW-m-200,Y=y,Width=200,Height=26,FontSize=15,IsBold=true,ColorHex=ac,TextAlignment="Right"});
            y+=26;
            A(new DesignerBlock{Id="address",X=m,Y=y,Width=cW*0.55,Height=16,FontSize=8,ColorHex=lbl});
            A(new DesignerBlock{Id="date",X=pW-m-200,Y=y,Width=200,Height=16,FontSize=10,ColorHex=lbl,TextAlignment="Right"});
            y+=16;
            A(new DesignerBlock{Id="phone",X=m,Y=y,Width=cW*0.55,Height=16,FontSize=8,ColorHex=lbl});
            y+=24;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=2,ColorHex=ac});
            y+=10;
            // Numbered sections in table form
            string[][] rows={
                new[]{"Customer","customer_name"},new[]{"Phone","customer_phone"},
                new[]{"Device","model"},new[]{"Brand","brand"},
                new[]{"Serial No","serial_number"},new[]{"Accessories","accessories"},
                new[]{"Est. Cost","cost"}
            };
            foreach(var r in rows){
                A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#CFD8DC"});
                A(new DesignerBlock{Id="custom_text",CustomText=r[0],X=m+4,Y=y+3,Width=120,Height=18,FontSize=8,IsBold=true,ColorHex=lbl});
                A(new DesignerBlock{Id=r[1],X=m+128,Y=y+3,Width=cW-132,Height=18,FontSize=10,ColorHex=txt});
                y+=22;
            }
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex="#CFD8DC"});
            y+=12;
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=20,ColorHex="#ECEFF1"});
            A(new DesignerBlock{Id="custom_text",CustomText="COMPLAINT / ISSUE",X=m+6,Y=y+3,Width=250,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            y+=20;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="issue",X=m+4,Y=y+5,Width=cW-8,Height=h?55:155,FontSize=10,ColorHex=txt});
            y+=(h?65:165);
            A(new DesignerBlock{Id="rect",X=m,Y=y,Width=cW,Height=20,ColorHex="#ECEFF1"});
            A(new DesignerBlock{Id="custom_text",CustomText="DIAGNOSTICS",X=m+6,Y=y+3,Width=250,Height=16,FontSize=8,IsBold=true,ColorHex=ac});
            y+=20;
            A(new DesignerBlock{Id="line",X=m,Y=y,Width=cW,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="diagnostics",X=m+4,Y=y+5,Width=cW-8,Height=h?35:90,FontSize=10,ColorHex="#333",IsItalic=true});
            double tY=pH-(h?80:120);
            A(new DesignerBlock{Id="line",X=m,Y=tY,Width=cW,Height=1,ColorHex="#CFD8DC"});
            A(new DesignerBlock{Id="terms",X=m,Y=tY+4,Width=cW,Height=h?22:40,FontSize=6,ColorHex="#AAA"});
            double sY=pH-38;
            A(new DesignerBlock{Id="line",X=m,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Customer Signature",X=m,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
            A(new DesignerBlock{Id="line",X=pW-m-190,Y=sY,Width=190,Height=1,ColorHex=ac});
            A(new DesignerBlock{Id="custom_text",CustomText="Authorized Signature",X=pW-m-190,Y=sY+3,Width=190,Height=16,FontSize=8,TextAlignment="Center",ColorHex="#888"});
        }

        public static string GetTemplateJson(string id) => JsonSerializer.Serialize(GetTemplateBlocks(id));
    }
}
