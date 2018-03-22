﻿using ImageClassifier.Interfaces.GlobalUtils.AzureStorage;
using ImageClassifier.Interfaces.GlobalUtils.Configuration;
using ImageClassifier.Interfaces.Source.LabeldBlobSource.Persistence;
using System;
using System.Linq;
using System.Collections.Generic;
using ImageClassifier.Interfaces.GlobalUtils;
using System.Windows;
using ImageClassifier.Interfaces.Source.BlobSource.Persistence;
using ImageClassifier.Interfaces.Source.BlobSource.UI;
using ImageClassifier.Interfaces.GenericUI;

namespace ImageClassifier.Interfaces.Source.LabeldBlobSource
{
    class LabeledAzureBlobSource : ConfigurationBase<AzureBlobStorageConfiguration>, IMultiImageDataSource
    {
        #region PrivateMembers
        private const int BATCH_SIZE = 6;

        private AzureBlobStorageConfiguration Configuration { get; set; }
        private StorageUtility AzureStorageUtils { get; set; }
        private LabelledBlobPersisteceLogger PersistenceLogger { get; set; }
        /// <summary>
        /// Index into CurrentImageList
        /// </summary>
        private int CurrentImage { get; set; }
        /// <summary>
        /// List of files from the currently selected catalog file
        /// </summary>
        private List<ScoringImage> CurrentImageList { get; set; }
        #endregion

        public LabeledAzureBlobSource()
            : base("LabeledAzureStorageConfiguration.json")
        {
            this.Name = "LabelledAzureStorageSource";
            this.SourceType = DataSourceType.LabelledBlob;
            this.DeleteSourceFilesWhenComplete = true;
            this.MultiClass = true;
            this.CurrentImage = -1;


            // Get the configuration specific to this instance
            this.Configuration = this.LoadConfiguration();

            // Create the storage utils
            this.AzureStorageUtils = new StorageUtility(this.Configuration);

            // Prepare the UI control with the right hooks.
            AzureStorageConfigurationUi configUi = new AzureStorageConfigurationUi(this, this.Configuration);
            configUi.OnConfigurationSaved += ConfigurationSaved;
            configUi.OnSourceDataUpdated += AcquireContent;

            this.ConfigurationControl =
                new ConfigurationControlImpl("Labelled Azure Storage Service",
                configUi);

            // Get a list of containers through the persistence logger 
            this.PersistenceLogger = new LabelledBlobPersisteceLogger(this.Configuration);
            this.CurrentContainer = this.Containers.FirstOrDefault();
            this.InitializeOnNewContainer();

            this.ContainerControl = new GenericContainerControl(this);
            this.ImageControl = new MultiImageControl(this);
        }

        #region IMultiImageDataSource

        public event OnContainerLabelsAcquired OnLabelsAcquired;

        public string Name { get; private set; }

        public DataSourceType SourceType { get; private set; }

        public bool DeleteSourceFilesWhenComplete { get; private set; }

        public bool MultiClass { get; private set; }

        public string CurrentContainer { get; private set; }

        public void ClearSourceFiles()
        {
            if(this.DeleteSourceFilesWhenComplete)
            {
                String downloadDirectory = System.IO.Path.Combine(this.Configuration.RecordLocation, "temp");
                FileUtils.DeleteFiles(downloadDirectory, new string[] { this.Configuration.FileType });
            }
        }

        public IEnumerable<string> Containers { get { return this.PersistenceLogger.LabelMap.Keys; } }

        public IDataSink Sink { get; set; }

        public IConfigurationControl ConfigurationControl { get; private set; }

        public IContainerControl ContainerControl { get; private set; }

        public IImageControl ImageControl { get; private set; }

        public void SetContainer(string container)
        {
            if (this.Containers.Contains(container) &&
                String.Compare(this.CurrentContainer, container) != 0)
            {
                this.CurrentContainer = container;
                this.InitializeOnNewContainer();
            }
        }

        public int CurrentContainerIndex { get { return this.CurrentImage; } }

        public int CurrentContainerCollectionCount { get { return this.CurrentImageList.Count(); } }

        public IEnumerable<string> CurrentContainerCollectionNames
        {
            get
            {
                List<string> itemNames = new List<string>();
                foreach (ScoringImage item in this.CurrentImageList)
                {
                    itemNames.Add(item.Url);
                }
                return itemNames;
            }
        }

        public bool CanMoveNext
        {
            get
            {
                return !(this.CurrentImage >= this.CurrentImageList.Count - 1);
            }
        }

        public bool CanMovePrevious
        {
            get
            {
                return !(this.CurrentImage < 0);
            }
        }

        public bool JumpToSourceFile(int index)
        {
            bool returnValue = true;
            String error = String.Empty;

            if (this.CurrentImageList == null || this.CurrentImageList.Count == 0)
            {
                error = "A colleciton must be present to use the Jump To function.";
            }
            else if (index > this.CurrentImageList.Count || index < 1)
            {
                error = String.Format("Jump to index must be within the collection size :: 1-{0}", this.CurrentImageList.Count);
            }
            else
            {
                this.CurrentImage = index - 2; // Have to move past the one before because next increments by 1
            }

            if (!String.IsNullOrEmpty(error))
            {
                System.Windows.MessageBox.Show(error, "Jump To Error", MessageBoxButton.OK, MessageBoxImage.Error);
                returnValue = false;
            }

            return returnValue;
        }

        public IEnumerable<SourceFile> NextSourceGroup()
        {
            List<SourceFile> returnFiles = new List<SourceFile>();
            if (this.CanMoveNext)
            {
                if (this.CurrentImage <= -1)
                {
                    this.CurrentImage = -1;
                }

                int count = 0;
                while (this.CanMoveNext && count++ < LabeledAzureBlobSource.BATCH_SIZE)
                {
                    ScoringImage image = this.CurrentImageList[++this.CurrentImage];

                    // Download blah blah
                    String token = this.AzureStorageUtils.GetSasToken(this.Configuration.StorageContainer);
                    String imageUrl = String.Format("{0}{1}", image.Url, token);

                    SourceFile returnFile = new SourceFile();
                    returnFile.Name = System.IO.Path.GetFileName(image.Url);
                    returnFile.DiskLocation = this.DownloadStorageFile(imageUrl);

                    if (this.Sink != null)
                    {
                        ScoredItem found = this.Sink.Find(this.CurrentContainer, image.Url);
                        if (found != null)
                        {
                            returnFile.Classifications = found.Classifications;
                        }
                    }

                    returnFiles.Add(returnFile);
                }
            }
            return returnFiles;
        }

        public IEnumerable<SourceFile> PreviousSourceGroup()
        {
            List<SourceFile> returnFiles = new List<SourceFile>();
            if (this.CanMovePrevious)
            {
                this.CurrentImage -= ( (2*LabeledAzureBlobSource.BATCH_SIZE) + 1);
                returnFiles.AddRange(this.NextSourceGroup());
            }
            return returnFiles;

        }

        public void UpdateSourceFile(SourceFile file)
        {
            ScoringImage image = this.CurrentImageList.FirstOrDefault(x => String.Compare(System.IO.Path.GetFileName(x.Url), file.Name, true) == 0);
            if (image != null && this.Sink != null)
            {
                ScoredItem item = new ScoredItem()
                {
                    Container = this.CurrentContainer,
                    Name = image.Url,
                    Classifications = file.Classifications
                };
                this.Sink.Record(item);
            }
        }
        #endregion

        #region Private Helpers

        private void InitializeOnNewContainer()
        {
            this.CurrentImage = -1;
            this.CurrentImageList = this.PersistenceLogger.LoadContainerData(this.CurrentContainer);
        }

        /// <summary>
        /// Triggered by the configuration UI when the underlying configuration is updated.
        /// 
        /// Performs work then bubbles the call out to the parent app (listener)
        /// </summary>
        /// <param name="caller">unused</param>
        private void ConfigurationSaved(object caller)
        {
            // Save the configuration
            this.SaveConfiguration(this.Configuration);
            // Update the storage utils
            this.AzureStorageUtils = new StorageUtility(this.Configuration);
            // Notify anyone who wants to be notified
            this.ConfigurationControl.OnConfigurationUdpated?.Invoke(this);
            this.OnLabelsAcquired?.Invoke(this.GetContainerLabels());

        }

        public IEnumerable<string> GetContainerLabels()
        {
            List<string> returnLabels = new List<string>();
            foreach(String container in this.Containers)
            {
                string cont = container.Trim(new char[] { '/' });

                int idx = cont.LastIndexOf('/');
                if(idx > 0)
                {
                    returnLabels.Add(cont.Substring(idx+1));
                }
                else
                {
                    returnLabels.Add(cont);
                }
            }
            return returnLabels;
        }

        private void AcquireContent(object caller)
        {
            // Update the data source
            if (System.Windows.MessageBox.Show(
                String.Format("This action will delete all files in : {0}{1}Further, the configuation will be updated.{1}Would you like to continue?",
                    this.Configuration.RecordLocation,
                    Environment.NewLine),
                "Acquire Storage File List",
                MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
            {
                return;
            }

            // Delete the ISink storage
            if (this.Sink != null)
            {
                this.Sink.Purge();
            }

            // add in the window to let them know we're working, see AzureBlobSource:260
            AcquireContentWindow contentWindow = new AcquireContentWindow();
            contentWindow.DisplayContent = String.Format("Acquiring {0} files from {1}", this.Configuration.FileCount, this.Configuration.StorageAccount);
            if (this.ConfigurationControl.Parent != null)
            {
                contentWindow.Top = this.ConfigurationControl.Parent.Top + (this.ConfigurationControl.Parent.Height - contentWindow.Height) / 2;
                contentWindow.Left = this.ConfigurationControl.Parent.Left + (this.ConfigurationControl.Parent.Width - contentWindow.Width) / 2;
            }
            contentWindow.Show();
             
            // Clean up current catalog data and reget the persistence logger
            FileUtils.DeleteFiles(this.Configuration.RecordLocation, new string[] { "*.csv" });
            this.PersistenceLogger = new LabelledBlobPersisteceLogger(this.Configuration);

            List<String> directories = new List<string>();
            foreach (String dir in this.AzureStorageUtils.ListDirectories(this.Configuration.StorageContainer, this.Configuration.BlobPrefix, false))
            {
                directories.Add(dir);
            }

            foreach(string directory in directories)
            {
                foreach (KeyValuePair<string, string> kvp in this.AzureStorageUtils.ListBlobs(this.Configuration.StorageContainer,
                    directory,
                    this.Configuration.FileType,
                    false))
                {
                    this.PersistenceLogger.RecordLabelledImage(directory, kvp.Value);
                }
            }

            // Close window saying we are downloading 
            contentWindow.Close();


            // Update class variables
            this.CurrentContainer = this.Containers.FirstOrDefault();

            // Get the new data
            this.InitializeOnNewContainer();

            this.OnLabelsAcquired?.Invoke(this.GetContainerLabels());
        }

        private String DownloadStorageFile(string imageUrl)
        {
            String downloadDirectory = System.IO.Path.Combine(this.Configuration.RecordLocation, "temp");
            FileUtils.EnsureDirectoryExists(downloadDirectory);

            string downloadFile = System.IO.Path.Combine(downloadDirectory, String.Format("{0}.jpg", (Guid.NewGuid().ToString("N"))));

            this.AzureStorageUtils.DownloadBlob(imageUrl, downloadFile);

            return downloadFile;
        }

        #endregion

    }
}