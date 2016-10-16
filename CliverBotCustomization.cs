//********************************************************************************************
//Author: Sergey Stoyan, CliverSoft.com
//        http://cliversoft.com
//        stoyan@cliversoft.com
//        sergey.stoyan@gmail.com
//        27 February 2007
//Copyright: (C) 2007, Sergey Stoyan
//********************************************************************************************

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;
using System.Data;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Net.Mail;
using Cliver;
using System.Configuration;
using System.Windows.Forms;
//using MySql.Data.MySqlClient;
using Cliver.Bot;
using Cliver.BotGui;
using Microsoft.Win32;
using System.Reflection;

namespace Cliver.BotCustomization
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            //Cliver.CrawlerHost.Linker.ResolveAssembly();
            main();
        }

        static void main()
        {
            //Cliver.Bot.Program.Run();//It is the entry when the app runs as a console app.
            Cliver.BotGui.Program.Run();//It is the entry when the app uses the default GUI.
        }
    }

    public class CustomBotGui : Cliver.BotGui.BotGui
    {
        override public string[] GetConfigControlNames()
        {
            return new string[] { "General", "Input", "Output", "Web", /*"Browser", "Spider",*/ "Proxy", "Log" };
        }

        override public Cliver.BaseForm GetToolsForm()
        {
            return null;
        }

        //override public Type GetBotThreadControlType()
        //{
        //    return typeof(IeRoutineBotThreadControl);
        //    //return typeof(WebRoutineBotThreadControl);
        //}
    }

    public class CustomBot : Cliver.Bot.Bot
    {
        new static public string GetAbout()
        {
            return @"WEB CRAWLER
Created: " + Cliver.Bot.Program.GetCustomizationCompiledTime().ToString() + @"
Developed by: www.cliversoft.com";
        }

        new static public void SessionCreating()
        {
            //InternetDateTime.CHECK_TEST_PERIOD_VALIDITY(2016, 10, 7);

            FileWriter.This.WriteHeader(
                "Individual Name",
                    "Company Name",
                    "City",
                    "State",
                    "Site",
                    "Phone",
                    "Mobile",
                    "Fax",
                    "Url",
                    "Id"
                    //"Json"
                    );
        }

        new static public void SessionClosing()
        {
        }

        override public void CycleBeginning()
        {
            //IR = new IeRoutine(((IeRoutineBotThreadControl)BotThreadControl.GetInstanceForThisThread()).Browser);
            //IR.UseCache = false;
            HR = new HttpRoutine();
        }

        //IeRoutine IR;

        HttpRoutine HR;

        public class CategoryItem : InputItem
        {
            readonly public string State;

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                string url = "https://mortgageapi.zillow.com/getLenderDirectoryListings?partnerId=RD-CZMBMCZ&sort=Relevance&pageSize=20&page=1&fields.0=individualName&fields.1=imageURL120x120Secure&fields.2=companyName&fields.3=totalReviews&fields.4=rating&fields.5=screenName&fields.6=imageURL120x120Secure&fields.7=individualName&fields.8=employerScreenName&location=" + State;
                //string url = "https://www.zillow.com/lender-directory/?location=" + WebUtility.UrlEncode(State);
                cb.process_category(url);
            }
        }

        void process_category(string url)
        {
            if (!HR.GetPage(url))
                throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

            DataSifter.Capture c = category.Parse(HR.HtmlResult);
            string[] ids = c.ValuesOf("Id");

            Match m = Regex.Match(url, @".*&pageSize=(?'PageSize'\d+)&page=(?'PageNumber'\d+)&");
            if (!m.Success)
                throw new Exception("Could not parse page number!");
            if (ids.Length == int.Parse(m.Groups["PageSize"].Value))
            {
                int pn = int.Parse(m.Groups["PageNumber"].Value);
                BotCycle.Add(new CategoryNextPageItem(Regex.Replace(url, @"&page=\d+&", "&page=" + (pn + 1) + "&")));
            }

            foreach (string id in ids)
                BotCycle.Add(new CompanyItem(id));
        }

        static DataSifter.Parser category = new DataSifter.Parser("category.fltr");

        public class CategoryNextPageItem : InputItem
        {
            readonly public string Url;

            public CategoryNextPageItem(string url)
            {
                Url = url;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                cb.process_category(Url);
            }
        }

        public class CompanyItem : InputItem
        {
            readonly public string LenderId;

            public CompanyItem(string lender_id)
            {
                LenderId = lender_id;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                //https://mortgageapi.zillow.com/getRegisteredLender?partnerId=RD-CZMBMCZ&amp;fields.0=individualName&amp;fields.1=address&amp;fields.2=cellPhone&amp;fields.3=companyName&amp;fields.4=faxPhone&amp;fields.5=individualName&amp;fields.6=nmlsType&amp;fields.7=officePhone&amp;fields.8=screenName&amp;fields.9=website&amp;lenderRef.lenderId=
                //https://mortgageapi.zillow.com/getRegisteredLender?partnerId=RD-CZMBMCZ&fields.0=individualName&fields.1=address&fields.2=cellPhone&fields.3=&fields.4=&fields.5=&fields.6=&fields.7=&fields.8=&fields.9=&lenderRef.lenderId=" + WebUtility.UrlEncode(LenderId);

                CustomBot cb = (CustomBot)bc.Bot;
                string url = "https://mortgageapi.zillow.com/getRegisteredLender?partnerId=RD-CZMBMCZ&fields.0=individualName&fields.1=address&fields.2=cellPhone&fields.3=companyName&fields.4=faxPhone&fields.5=individualName&fields.6=nmlsType&fields.7=officePhone&fields.8=screenName&fields.9=website&lenderRef.lenderId=" + WebUtility.UrlEncode(LenderId);
                if (!cb.HR.GetPage(url))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

                DataSifter.Capture c = CustomBot.company.Parse(cb.HR.HtmlResult);

                string individual_name = "";
                DataSifter.Capture cp = c.FirstOf("individualName");
                if (cp != null)
                    individual_name = cp.ValueOf("firstName") + " " + cp.ValueOf("lastName");

                string mobile = "";
                cp = c.FirstOf("cellPhone");
                if (cp != null)
                    mobile = "(" + cp.ValueOf("areaCode") + ") " + cp.ValueOf("prefix") + "-" + cp.ValueOf("number");

                string phone = "";
                cp = c.FirstOf("officePhone");
                if (cp != null)
                    phone = "(" + cp.ValueOf("areaCode") + ") " + cp.ValueOf("prefix") + "-" + cp.ValueOf("number");

                string fax = "";
                cp = c.FirstOf("faxPhone");
                if (cp != null)
                    fax = "(" + cp.ValueOf("areaCode") + ") " + cp.ValueOf("prefix") + "-" + cp.ValueOf("number");

                FileWriter.This.PrepareAndWriteLineWithHeader(
                    "Individual Name", individual_name,
                    "Company Name", c.ValueOf("companyName"),
                    "City", c.ValueOf("city"),
                    "State", c.ValueOf("stateAbbreviation"),
                    "Site", c.ValueOf("website"),
                    "Phone", phone,
                    "Mobile", mobile,
                    "Fax", fax,
                    "Url", "https://www.zillow.com/lender-profile/" + c.ValueOf("screenName") + "/",
                    "Id", LenderId
                    //"Json", url
                    );
            }
        }
        static DataSifter.Parser company = new DataSifter.Parser("company.fltr");
    }
}