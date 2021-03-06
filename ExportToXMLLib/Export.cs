﻿using EPDM.Interop.epdm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Xml;
using System.Text;
using System.IO;

namespace ExportToXMLLib
{
    public class Export
    {
        const int BoomId = 8;
        private static object mLockObj = new object();
        DBConnection con = DBConnection.DBProp;
        
        private List<string> conf;
        private List<MyBomShell> AssmblyBom;
        private IEnumerable<MyBomShell> fullDataSpecParts;
        private List<MyBomShell> fullDataSpecAsmblAndParts;
        private string filePath;
        private string pathToSave = @"\\pdmsrv\XML\";
        
        public Export(string filePath)
        {
            this.filePath = filePath;
            conf = GetConfigurations(filePath);
            AssmblyBom = GetBomShell(filePath, conf, BoomId);
            fullDataSpecParts = GetFullSpecification();//для каждой детали
        }
        


        private static IEdmVault5 vault
        {
            get
            {
                IEdmVault5 vault = EdmVaultSingleton.Instance;

                if (!vault.IsLoggedIn)
                {
                    vault.LoginAuto("Vents-PDM", 0);
                }
                return vault;
            }
        }

        private void ExportPartsToXML()
        {
            foreach (var part in fullDataSpecParts.GroupBy(x => x.FileName))
            {
                string name = Path.GetFileNameWithoutExtension(part.Key);
                var myXml = new XmlTextWriter(pathToSave + name + ".xml", Encoding.UTF8);
                myXml.WriteStartDocument();
                myXml.Formatting = Formatting.Indented;
            
                myXml.WriteStartElement("xml");
                myXml.WriteStartElement("transactions");
                myXml.WriteStartElement("transaction");


                myXml.WriteAttributeString("vaultName", "Vents-PDM");
                myXml.WriteAttributeString("type", "");
                myXml.WriteAttributeString("date", "");

                // document
                myXml.WriteStartElement("document");
                myXml.WriteAttributeString("pdmweid", "");
                myXml.WriteAttributeString("aliasset", "Export To ERP");
                
                foreach (var item in part)
                {      
                    // Конфигурация
                    myXml.WriteStartElement("configuration");
                    myXml.WriteAttributeString("name", item.Configuration);

                    // Материал
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Материал");
                    myXml.WriteAttributeString("value", item.Material.ToString());
                    myXml.WriteEndElement();

                    // Наименование
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Наименование");
                    myXml.WriteAttributeString("value", item.Description);
                    myXml.WriteEndElement();

                    // Обозначение
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Обозначение");
                    myXml.WriteAttributeString("value", item.PartNumber);
                    myXml.WriteEndElement();

                    // Площадь покрытия
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Площадь покрытия");
                    myXml.WriteAttributeString("value", item.SurfaceArea.ToString());
                    myXml.WriteEndElement();

                    // Код_Материала
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Код_Материала");
                    myXml.WriteAttributeString("value", item.CodeMaterial.ToString());
                    myXml.WriteEndElement();

                    // Длина граничной рамки
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Длина граничной рамки");
                    myXml.WriteAttributeString("value", item.WorkpieceY.ToString());
                    myXml.WriteEndElement();

                    // Ширина граничной рамки
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Ширина граничной рамки");
                    myXml.WriteAttributeString("value", item.WorkpieceX.ToString());
                    myXml.WriteEndElement();

                    // Сгибы
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Сгибы");
                    myXml.WriteAttributeString("value", item.Bend.ToString());
                    myXml.WriteEndElement();

                    // Толщина листового материала
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Толщина листового материала");
                    myXml.WriteAttributeString("value", item.ListThickness.ToString());
                    myXml.WriteEndElement();

                    // PaintX
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "PaintX");
                    myXml.WriteAttributeString("value", item.PaintX.ToString());
                    myXml.WriteEndElement();

                    // PaintY
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "PaintY");
                    myXml.WriteAttributeString("value", item.PaintY.ToString());
                    myXml.WriteEndElement();

                    // PaintZ
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "PaintZ");
                    myXml.WriteAttributeString("value", item.PaintZ.ToString());
                    myXml.WriteEndElement();

                    // Версия
                    myXml.WriteStartElement("attribute");
                    myXml.WriteAttributeString("name", "Версия");
                    myXml.WriteAttributeString("value", item.LastVersion.ToString());
                    myXml.WriteEndElement();
                    myXml.WriteEndElement();// ' элемент Configuration name
                }

                myXml.WriteEndElement(); // ' элемент DOCUMENT
                myXml.WriteEndElement(); // ' элемент TRANSACTION
                myXml.WriteEndElement(); // ' элемент TRANSACTIONS
                myXml.WriteEndElement(); // ' элемент XML

                myXml.Flush();
                myXml.Close();
            }
        }
        private void ExportToXMLWithSubAsmbl(List<MyBomShell> list, string nameAddintion, int param)
        {
            int currentTreeLevel;
            int helpCount = 0;
            int previousTreeLevel = 0;
            string type = null;
            bool p = false;
            bool f = false;

            int l = Convert.ToInt32(list[0].FileName.Count()) - 7;
            string fileName = list[0].FileName.Substring(0, l);
            

            var myXml = new XmlTextWriter(pathToSave + fileName + nameAddintion + ".xml", Encoding.UTF8);
            myXml.WriteStartDocument();
            myXml.Formatting = Formatting.Indented;

            myXml.WriteStartElement("xml");
            myXml.WriteStartElement("transactions");
            myXml.WriteStartElement("transaction");

            myXml.WriteAttributeString("vaultName", "Vents-PDM");
            myXml.WriteAttributeString("type", "");
            myXml.WriteAttributeString("date", "");

            // document
            myXml.WriteStartElement("document");
            myXml.WriteAttributeString("pdmweid", "");
            myXml.WriteAttributeString("aliasset", "Export To ERP");

            foreach (var it in list)
            {
                currentTreeLevel = (int)it.TreeLevel + param;

                if (helpCount != 0)
                {                    
                        if (previousTreeLevel > currentTreeLevel && type == "sldasm")//переход на уровень выше
                        { 
                            myXml.WriteEndElement(); //configurations 
                            myXml.WriteEndElement(); //configurations 
                            myXml.WriteEndElement(); //references
                            myXml.WriteEndElement(); //document alias 
                            f = true;
                        }
                        /*else if (previousTreeLevel < currentTreeLevel) //следующий элемент вложенный
                        {
                            
                        }*/
                        if (type == "sldasm" && it.FileType == type && previousTreeLevel == currentTreeLevel)// если две сборки подряд одного уровня
                        {
                            if (currentTreeLevel != 0)
                            {
                                // myXml.WriteEndElement();//references
                                myXml.WriteEndElement();//configurations
                                p = true;
                            }
                            else
                            {
                                myXml.WriteEndElement();//document alias
                                myXml.WriteEndElement();//references
                                myXml.WriteEndElement();//configurations
                                p = true;
                            }         
                        }
                        if (type == "sldasm" && previousTreeLevel == currentTreeLevel)
                        {
                            if (p == false)
                            {
                                myXml.WriteEndElement();//configurations
                            }
                        }
                    if (currentTreeLevel == 0 && type == "sldprt")
                    {
                        if (p == false)
                        {
                            if(f == false)
                            {
                                myXml.WriteEndElement();//document alias
                                myXml.WriteEndElement();//references
                                myXml.WriteEndElement();//configurations
                            }
                        }
                    }
                    p = false;
                    helpCount--;
                }

                #region XML
                // Конфигурация
                myXml.WriteStartElement("configuration");
                myXml.WriteAttributeString("name", it.Configuration);

                // Версия
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Версия");
                myXml.WriteAttributeString("value", it.LastVersion.ToString());
                myXml.WriteEndElement();

                // Масса
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Масса");
                myXml.WriteAttributeString("value", it.Weight.ToString());
                myXml.WriteEndElement();

                // Наименование
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Наименование");
                myXml.WriteAttributeString("value", it.Description);
                myXml.WriteEndElement();

                // Обозначение
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Обозначение");
                myXml.WriteAttributeString("value", it.PartNumber);
                myXml.WriteEndElement();

                // Раздел
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Раздел");
                myXml.WriteAttributeString("value", it.Partition.ToString());
                myXml.WriteEndElement();

                // ERP code
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "ERP code");
                myXml.WriteAttributeString("value", it.ErpCode.ToString());
                myXml.WriteEndElement();

                // Код_Материала
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Код_Материала");
                myXml.WriteAttributeString("value", it.CodeMaterial.ToString());
                myXml.WriteEndElement();

                // Код Документа
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Код Документа");
                myXml.WriteAttributeString("value", "");
                myXml.WriteEndElement();

                // Кол. Материала
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Кол. Материала");
                myXml.WriteAttributeString("value", it.SummMaterial.ToString());
                myXml.WriteEndElement();

                // Состояние 
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Состояние");
                myXml.WriteAttributeString("value", "");
                myXml.WriteEndElement();

                // Подсчет ссылок
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Подсчет ссылок");
                myXml.WriteAttributeString("value", it.Quantity.ToString());
                myXml.WriteEndElement();

                // Конфигурация
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Конфигурация");
                myXml.WriteAttributeString("value", it.Configuration);
                myXml.WriteEndElement();

                // Идентификатор
                myXml.WriteStartElement("attribute");
                myXml.WriteAttributeString("name", "Идентификатор");
                myXml.WriteAttributeString("value", "");
                myXml.WriteEndElement();

                #endregion

                if (it.FileType == "sldasm")
                {
                    if (currentTreeLevel == 0)
                    {
                        myXml.WriteStartElement("references");
                        myXml.WriteStartElement("document");
                        myXml.WriteAttributeString("pdmweid", "");
                        myXml.WriteAttributeString("aliasset", "Export To ERP");
                    }

                    type = "sldasm";
                }
                else if (it.FileType == "sldprt")
                {
                    myXml.WriteEndElement();//configurations
                    type = "sldprt";
                }
                helpCount++;
                previousTreeLevel = currentTreeLevel;

            }

            myXml.WriteEndElement(); // ' элемент DOCUMENT
            myXml.WriteEndElement(); // ' элемент TRANSACTION
            myXml.WriteEndElement(); // ' элемент TRANSACTIONS
            myXml.WriteEndElement(); // ' элемент XML

            myXml.Flush();
            myXml.Close();
        }


        private List<string> GetConfigurations(string filePath)
        {
            IEdmFolder5 oFolder;

            var edmFile5 = vault.GetFileFromPath(filePath, out oFolder);
            EdmStrLst5 cfgList = edmFile5.GetConfigurations(0);

            var headPosition = cfgList.GetHeadPosition();
            List<string> configsArr = new List<string>();

            while (!headPosition.IsNull)
            {
                var configName = cfgList.GetNext(headPosition);
                if (configName != "@")
                {
                    configsArr.Add(configName);
                }
            }
            return configsArr;
        }
        private List<MyBomShell> GetBomShell(string filePath, List<string> Configurations, int BoomId)
        {
            try
            {
                List<MyBomShell> BomShellList = new List<MyBomShell>();
                MyBomShell bom = null;


                IEdmFolder5 oFolder;
                IEdmFile7 EdmFile7 = (IEdmFile7)vault.GetFileFromPath(filePath, out oFolder);

                foreach (var refConfig in Configurations)
                {
                    EdmBomView bomView = EdmFile7.GetComputedBOM(BoomId, -1, refConfig, 3);
                    if (bomView == null)
                    {
                        throw new Exception("Computed BOM it can not be null");
                    }
                    object[] bomRows;
                    EdmBomColumn[] bomColumns;
                    bomView.GetRows(out bomRows);
                    bomView.GetColumns(out bomColumns);

                    for (var i = 0; i < bomRows.Length; i++)
                    {
                        List<object> eachItem = new List<object>();
                        IEdmBomCell cell = (IEdmBomCell)bomRows.GetValue(i);
                        int treeLevel = cell.GetTreeLevel();
                        for (var j = 0; j < bomColumns.Length; j++)
                        {
                            EdmBomColumn column = (EdmBomColumn)bomColumns.GetValue(j);
                            object value;
                            object computedValue;
                            string config;
                            bool readOnly;
                            cell.GetVar(column.mlVariableID, column.meType, out value, out computedValue, out config, out readOnly);
                            eachItem.Add(value);
                        }
                        if (eachItem.Count > 0)
                        {
                            bom = new MyBomShell()
                            {
                                Partition = eachItem[0].ToString(),
                                PartNumber = eachItem[1].ToString(),
                                Description = eachItem[2].ToString(),
                                Material = eachItem[3].ToString(),
                                CMIMaterial = eachItem[4].ToString(),
                                ListThickness = eachItem[5].ToString(),
                                Quantity =(eachItem[6]).ToString(),//?
                                FileType = eachItem[7].ToString(),
                                Configuration = refConfig,
                                LastVersion = Convert.ToInt32(eachItem[9].ToString()),//?
                                IdPdm = Convert.ToInt32(eachItem[10]),
                                FileName = eachItem[11].ToString(),
                                FilePath = eachItem[12].ToString(),
                                ErpCode = eachItem[13].ToString(),
                                SummMaterial = eachItem[14].ToString(),
                                Weight = eachItem[15].ToString(),
                                CodeMaterial = eachItem[16].ToString(),
                                Format = eachItem[17].ToString(),
                                Note = eachItem[18].ToString(),
                                RefConfig = eachItem[8].ToString(),
                                TreeLevel = treeLevel
                            };
                            BomShellList.Add(bom);
                        }
                        else
                        {
                            eachItem = new List<object>();
                        }
                    }
                }
                return BomShellList;
            }
            catch (COMException ex)
            {
                //MessageBox.Show("Failed get bom shell " + (EdmResultErrorCodes_e)ex.ErrorCode + ". Укажите вид PDM или тип спецификации");
                throw ex;
            }
        }
        private IEnumerable<MyBomShell> GetFullSpecification()
        {
            IEnumerable<MyBomShell> spec = from data in AssmblyBom
                                         join parts in con.ViewParts
                                         on new { id = data.IdPdm, conf = data.Configuration, version = (int)data.LastVersion }
                                         equals new { id = parts.IDPDM , conf = parts.ConfigurationName, version = parts.Version }
                                         into fullSpec
                                         from f in fullSpec.DefaultIfEmpty()
                                          

                                         select new MyBomShell
                                         {
                                             CMIMaterial = data.CMIMaterial,
                                             CodeMaterial = data.CodeMaterial,
                                             Configuration = data.RefConfig,
                                             Description = data.Description,
                                             ErpCode = data.ErpCode,
                                             FileName = data.FileName,
                                             FilePath = data.FilePath,
                                             FileType = data.FileType,
                                             FolderPath = data.FolderPath,
                                             Format = data.Format,
                                             IdPdm = (f == null) ? 0 : f.IDPDM,
                                             LastVersion = (data.LastVersion == null) ? 0 : data.LastVersion,
                                             ListThickness = (f == null) ? string.Empty : f.Thickness.ToString(),
                                             Material = data.Material,
                                             Note = data.Note,
                                             ObjectType = data.ObjectType,
                                             Partition = data.Partition,
                                             PartNumber = data.PartNumber,
                                             Quantity = data.Quantity,
                                             RefConfig = data.Configuration,
                                             SummMaterial = data.SummMaterial,
                                             TreeLevel = (f == null) ? 0 : data.TreeLevel,
                                             Weight = data.Weight,
                                             Bend = (f == null) ? string.Empty : f.Bend.ToString(),
                                             PaintX = (f == null) ? string.Empty : f.PaintX.ToString(),
                                             PaintY = (f == null) ? string.Empty : f.PaintY.ToString(),
                                             PaintZ = (f == null) ? string.Empty : f.PaintZ.ToString(),
                                             DXF = (f == null) ? string.Empty : f.DXF,
                                             SurfaceArea = (f == null) ? string.Empty : f.SurfaceArea.ToString(),
                                             WorkpieceX = (f == null) ? string.Empty : f.WorkpieceX.ToString(),
                                             WorkpieceY = (f == null) ? string.Empty : f.WorkpieceY.ToString()
                                         };            
            return spec;
        }

        private void AssmblAndAll_1_Level()
        {
            int maxAssmblLevel;
            fullDataSpecAsmblAndParts = new List<MyBomShell>();
            List<MyBomShell> listForEveryPartTemp = new List<MyBomShell>();

            GetMaxTreeLevel(out maxAssmblLevel);

            List<List<MyBomShell>> g = new List<List<MyBomShell>>();
            List<string> namesItem = new List<string> { };
            int index = 0;
            foreach (var item in AssmblyBom.Where(x=>x.FileType == "sldasm").GroupBy(x => x.FileName))
            {
                g.Add(new List<MyBomShell>());
                namesItem.Add(item.Key);
            }

            for (int i = 0; i < (maxAssmblLevel + 1); i++)//по каждому уровню
            {
                    foreach (var item in AssmblyBom)
                    {
                        if (item.FileType == "sldasm" && (item.TreeLevel == i || item.TreeLevel == (i + 1)))
                        {
                            if(item.TreeLevel == i)
                            {
                                index = namesItem.IndexOf(item.FileName);
                            }
                            g[index].Add(item);
                        }
                        else if (item.FileType == "sldprt" && (item.TreeLevel == (i + 1)))
                        {
                            g[index].Add(item);
                        }
                    }
            }
            for(int i = 0; i < g.Count; i++)
            {                
                ExportToXMLWithSubAsmbl(g[i], "", (0 - (int)g[i][0].TreeLevel));
            }
            g.Clear();
        }
        private List<MyBomShell> AssmblAndAllDetails()
        {
            fullDataSpecAsmblAndParts = new List<MyBomShell>();
            foreach (var item in AssmblyBom)
            {
                if (item.FileType == "sldasm" && item.TreeLevel == 0)
                {
                    fullDataSpecAsmblAndParts.Add(item);
                }
                else if (item.FileType.Equals("sldprt"))
                {
                    

                    fullDataSpecAsmblAndParts.Add(item);
                }
            }
            return fullDataSpecAsmblAndParts;
        }
        public void XML()
        {
            if (filePath.ToUpper().Contains("SLDPRT"))
            {
                ExportPartsToXML();
            }
            else
            {
                ExportPartsToXML();

                AssmblAndAll_1_Level();

                ExportToXMLWithSubAsmbl(AssmblAndAllDetails(), " Parts", 0);
            }
        }
        private void GetMaxTreeLevel(out int max)
        {
            List<int> list = new List<int>();

            foreach (var item in AssmblyBom.Where(x=>x.FileType == "sldasm"))
            {
                list.Add((int)item.TreeLevel);
            }
            max = list.Max();
        }
        
    }

    public class EdmVaultSingleton
    {
        private static EdmVault5 mInstance = null;
        private static object mLockObj = new object();

        public static EdmVault5 Instance
        {
            get
            {
                try
                {
                    if (mInstance == null)
                    {
                        Monitor.Enter(mLockObj);
                        if (mInstance == null)
                        {
                            mInstance = new EdmVault5();
                        }
                        Monitor.Exit(mLockObj);
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Exit(mLockObj);
                }
                return mInstance;
            }
        }
    }    
}