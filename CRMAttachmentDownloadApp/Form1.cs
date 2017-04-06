using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Serialization;
using System.Reflection;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Microsoft.Crm.Sdk;
using Microsoft.Crm.Sdk.Query;
using Microsoft.Crm.Sdk.Metadata;
using Microsoft.Crm.SdkTypeProxy;
using Microsoft.Crm.SdkTypeProxy.Metadata;

namespace CRMAttachmentDownloadApp
{
    public partial class Form1 : Form
    {
        #region Initalization Section
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        #endregion

        #region Communication Methods
        /// <summary>
        /// Method to create an instance of the 
        /// Webservice based on the login criteria 
        /// specified by the user
        /// </summary>
        /// <returns></returns>
        public CrmService GetCrmService()
        {
            CrmAuthenticationToken loToken = new CrmAuthenticationToken();
            loToken.AuthenticationType = 0;
            loToken.OrganizationName = txtOrganization.Text;

            CrmService loService = new CrmService();
            loService.CrmAuthenticationTokenValue = loToken;

            System.Net.NetworkCredential loCredentials = new System.Net.NetworkCredential();
            loCredentials.Domain = txtDomain.Text;
            loCredentials.UserName = txtUsername.Text;
            loCredentials.Password = txtPassword.Text;
            loService.Credentials = loCredentials;

            loService.CrmAuthenticationTokenValue = loToken;
            loService.Url = "http://" + txtServer.Text + ":" + txtPort.Text + "/mscrmservices/2007/crmservice.asmx";
            return loService;
        }

        /// <summary>
        /// Method to Get a Record and return
        /// it in the form of a Dynamic Entity Object
        /// to the calling function
        /// </summary>
        /// <param name="poGuid"></param>
        /// <param name="psEntityName"></param>
        /// <param name="psAttributeName"></param>
        /// <param name="poService"></param>
        /// <param name="paColumnSet"></param>
        /// <returns></returns>
        public static DynamicEntity GetDynamicEntityBasedOnGuid(Guid poGuid, string psEntityName, string psAttributeName, CrmService poService, ArrayList paColumnSet)
        {
            CrmService loService = poService;
            QueryExpression loQuery = new QueryExpression();
            DynamicEntity loDynamicEntity = null;
            ColumnSet loColSet = new ColumnSet();

            foreach (string lsColumnItem in paColumnSet)
            {
                loColSet.AddColumn(lsColumnItem);
            }

            try
            {
                ConditionExpression loCondition = new ConditionExpression(psAttributeName, ConditionOperator.Equal, poGuid);
                FilterExpression loFilter = new FilterExpression();

                loQuery.EntityName = psEntityName;
                loQuery.ColumnSet = loColSet;
                loFilter.Conditions.Add(loCondition);

                loQuery.Criteria = loFilter;

                RetrieveMultipleRequest loRetrieve = new RetrieveMultipleRequest();

                loRetrieve.Query = loQuery;
                loRetrieve.ReturnDynamicEntities = true;
                RetrieveMultipleResponse loResponse = (RetrieveMultipleResponse)loService.Execute(loRetrieve);

                if (loResponse.BusinessEntityCollection.BusinessEntities.Count > 0)
                {
                    loDynamicEntity = (DynamicEntity)loResponse.BusinessEntityCollection.BusinessEntities[0];
                }
            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                MessageBox.Show("Error: " + ex.Detail.InnerXml.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message.ToString());
            }

            return loDynamicEntity;
        }

        /// <summary>
        /// Function to Check if Connection 
        /// fields contain a valid value
        /// </summary>
        /// <param name="lsValidationError"></param>
        /// <returns></returns>
        public bool FieldsContainValue(ref string lsValidationError)
        {
            bool lbReturnValue = true;

            if (txtServer.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Server Name";
            }
            else if (txtPort.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Server Port";
            }
            else if (txtOrganization.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Organization Name";
            }
            else if (txtUsername.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Username";
            }
            else if (txtPassword.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Password";
            }
            else if (txtDomain.Text == string.Empty)
            {
                lbReturnValue = false;
                lsValidationError = "Please enter Domain Name";
            }

            return lbReturnValue;
        }    
        #endregion

        #region Utility Methods
        public string AppendToFileName(string psFileName, string psAppendText, string psSeparator)
        {
            string lsReturnValue = string.Empty;
            string lsFileName = psFileName.Substring(0, psFileName.LastIndexOf("."));
            string lsFileExtension = psFileName.Substring(psFileName.LastIndexOf(".") + 1);

            lsReturnValue = lsFileName + psSeparator + psAppendText + "." + lsFileExtension;

            return lsReturnValue;
        }
        #endregion

        /// <summary>
        /// Download Button Click Event
        /// GUI Method to Connect to CRM 4.0 Webservice and 
        /// download all the files specified from the database
        /// saving them into the directory specified by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDownload_Click(object sender, EventArgs e)
        {
            string lsValidationError = string.Empty;
            Int32 liDuplicateCounter = 1;

            if (FieldsContainValue(ref lsValidationError) == true)
            {
                Cursor.Current = Cursors.WaitCursor;

                try
                {
                    string[] laAnnotationIds = txtGUID.Text.Split('\n');

                    // Establish a Connection with CRM ---------------------------
                    CrmService loService = GetCrmService();

                    // Get Annotation ID -----------------------------------------
                    // Guid loAnnotationId = new Guid(txtGUID.Text);

                    // Define columns to retrieve from annotation record ---------
                    ArrayList loColumnNames = new ArrayList();
                    loColumnNames.Add("filename");
                    loColumnNames.Add("documentbody");
                    string lsErrorGUID = "An Error Occured Processing the following GUID: \r\n";
                    bool lbErrorOccured = false;

                    foreach (string lsAnnotationString in laAnnotationIds)
                    {
                        try
                        {
                            if (lsAnnotationString != string.Empty)
                            {
                                string lsAnnotationString2 = lsAnnotationString.Replace("\r", "");
                                lsAnnotationString2 = lsAnnotationString2.Replace("{", "");
                                lsAnnotationString2 = lsAnnotationString2.Replace("}", "");

                                Guid loAnnotationId = new Guid(lsAnnotationString2);

                                // Retrieve the annotation record ----------------------------
                                DynamicEntity loDynamicEntity = GetDynamicEntityBasedOnGuid(loAnnotationId, "annotation", "annotationid", loService, loColumnNames);

                                string lsNameOfFileToBeSaved = string.Empty;
                                lsNameOfFileToBeSaved = loDynamicEntity.Properties["filename"].ToString();

                                if (File.Exists(txtPath.Text + lsNameOfFileToBeSaved))
                                {
                                    // Rename File Name to avoid overwriting
                                    lsNameOfFileToBeSaved = AppendToFileName(lsNameOfFileToBeSaved, liDuplicateCounter.ToString(), "_");
                                    liDuplicateCounter++;
                                }

                                // Download the attachment in the current execution folder.
                                using (FileStream fileStream = new FileStream(txtPath.Text + lsNameOfFileToBeSaved, FileMode.OpenOrCreate))
                                {
                                    //byte[] fileContent = new UTF8Encoding(true).GetBytes(loDynamicEntity.Properties["documentbody"].ToString());
                                    byte[] fileContent = Convert.FromBase64String(loDynamicEntity.Properties["documentbody"].ToString());
                                    fileStream.Write(fileContent, 0, fileContent.Length);
                                }
                            }
                        }
                        catch
                        {
                            lsErrorGUID += lsAnnotationString + "\n";
                            lbErrorOccured = true;
                        }
                    }

                    if (lbErrorOccured == true)
                    {
                        txtError.Text = lsErrorGUID;
                    }
                    MessageBox.Show("Download Completed!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.ToString());
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
            else
            {
                MessageBox.Show(lsValidationError);
            }
            
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {

            folderBrowserDialog1.ShowDialog();
            txtPath.Text = folderBrowserDialog1.SelectedPath.ToString() + "\\";
        }
    }
}
