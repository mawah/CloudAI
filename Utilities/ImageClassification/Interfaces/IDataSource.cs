﻿//
// Copyright  Microsoft Corporation ("Microsoft").
//
// Microsoft grants you the right to use this software in accordance with your subscription agreement, if any, to use software 
// provided for use with Microsoft Azure ("Subscription Agreement").  All software is licensed, not sold.  
// 
// If you do not have a Subscription Agreement, or at your option if you so choose, Microsoft grants you a nonexclusive, perpetual, 
// royalty-free right to use and modify this software solely for your internal business purposes in connection with Microsoft Azure 
// and other Microsoft products, including but not limited to, Microsoft R Open, Microsoft R Server, and Microsoft SQL Server.  
// 
// Unless otherwise stated in your Subscription Agreement, the following applies.  THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT 
// WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL MICROSOFT OR ITS LICENSORS BE LIABLE 
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED 
// TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) 
// HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE SAMPLE CODE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
//

using System.Collections.Generic;

namespace ImageClassifier.Interfaces
{

    /// <summary>
    /// DataSource types 
    /// </summary>
    public enum DataSourceType
    {
        Blob,
        Disk
    };

    /// <summary>
    /// SourceFile is used to pass data back and forth about a particular 
    /// item/image. 
    /// </summary>
    public class SourceFile
    {
        /// <summary>
        /// Display name of the item
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The full local disk path of the item. Source may have to 
        /// retrieve this from a network. Use the IDataSource.DeleteSourceFilesWhenComplete
        /// to determine if deleting when complete is acceptable.
        /// </summary>
        public string DiskLocation { get; set; }
        /// <summary>
        /// Applied classifications to the object (if any)
        /// </summary>
        public List<string> Classifications { get; set; }

        public SourceFile()
        {
            this.Classifications = new List<string>();
        }
    }

    /// <summary>
    /// IDataSource is an object that can 
    /// 1. COnfigure itself
    /// 2. Collect data from the source system to view
    /// 3. Track the classifiations provided by the user
    /// </summary>
    public interface IDataSource
    {
        #region General
        /// <summary>
        /// Display name of the provider
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Data type of the provider
        /// </summary>
        DataSourceType SourceType { get;  }
        /// <summary>
        /// Configuration UI with delegates for notifying
        /// parent when changes occur
        /// </summary>
        IConfigurationControl ConfigurationControl { get; }
        /// <summary>
        /// Sink for outputting data
        /// </summary>
        IDataSink Sink { get; set; }
        /// <summary>
        /// Indicates whether the file at SourceFile.DiskLocation can 
        /// be deleted when complted
        /// </summary>
        bool DeleteSourceFilesWhenComplete { get; }
        /// <summary>
        /// Flag indicating if multi class is supported for items
        /// </summary>
        bool MultiClass { get; }
        #endregion

        #region Containers
        /// <summary>
        ///  Current container being used
        /// </summary>
        string CurrentContainer { get; }
        /// <summary>
        /// List of all available containers
        /// </summary>
        IEnumerable<string> Containers { get; }
        /// <summary>
        /// Set a specific container as active
        /// </summary>
        /// <param name="container">Name of existing container to set active</param>
        void SetContainer(string container);
        /// <summary>
        /// Current index into the current container collection
        /// </summary>
        int CurrentContainerIndex { get; }
        /// <summary>
        /// Count of items in the current container selection
        /// </summary>
        int CurrentContainerCollectionCount { get; }
        IEnumerable<string> CurrentContainerCollectionNames { get; }
        #endregion

        #region Items
        /// <summary>
        /// Flag indicating if a move to the previous item in 
        /// the current container collection is valid.
        /// </summary>
        bool CanMovePrevious { get; }
        /// <summary>
        /// Request the previous item in the current container
        /// collection.
        /// </summary>
        /// <returns>SourceFile indicating information for the previous item</returns>
        SourceFile PreviousSourceFile();

        /// <summary>
        /// Flag indicating if a move to the next item in 
        /// the current container collection is valid.
        /// </summary>
        bool CanMoveNext { get; }
        /// <summary>
        /// Request the next item in the current container
        /// collection.
        /// </summary>
        /// <returns>SourceFile indicating information for the next item</returns>
        SourceFile NextSourceFile();

        /// <summary>
        /// Jump to another point in the current container collection
        /// </summary>
        /// <param name="idx">An index to the collection</param>
        /// <returns>True if the jump succeeded</returns>
        bool JumpToSourceFile(int idx);

        /// <summary>
        /// Force an update to whatever is used for storage to the 
        /// provided item.
        /// </summary>
        /// <param name="file">Item in the current container collection
        /// to update. Updates will be persisted during the call. </param>
        void UpdateSourceFile(SourceFile file);
        #endregion
    }
}
