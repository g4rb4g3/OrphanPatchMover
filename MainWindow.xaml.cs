using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WindowsInstaller;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace OrphanPatchMover
{
  /// <summary>
  /// Interaktionslogik für MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {

    readonly double MEGABYTE = 1048576;
    readonly int ACTION_DELETE = 0;
    readonly int ACTION_MOVE = 1;

    List<OrphanedItem> _unusedPatchFiles;
    bool _cancelProcess = false;
    CheckBox cbAllOrphaned = null;

    GridViewColumnHeader _lastHeaderClicked;
    ListSortDirection _lastDirection;
    
    public MainWindow()
    {
      InitializeComponent();
    }

    private void init()
    {
      mainWindow.IsEnabled = false;
      tbWait.Visibility = System.Windows.Visibility.Visible;
      new Thread(delegate()
      {
        findUnusedPatchFiles();
        this.Dispatcher.Invoke(delegate() {
          orphanedList.DataContext = new OrphanedItemsViewModel(_unusedPatchFiles);
          refreshOrphanedInfos();
          tbWait.Visibility = System.Windows.Visibility.Hidden;
          mainWindow.IsEnabled = true;

          if (orphanedList.ItemsSource != null)
          {
            (CollectionViewSource.GetDefaultView(orphanedList.ItemsSource) as ICollectionView).SortDescriptions.Clear();
            (CollectionViewSource.GetDefaultView(orphanedList.ItemsSource) as ICollectionView).SortDescriptions.Add(new SortDescription { PropertyName = "size", Direction = ListSortDirection.Descending });
          }
        }, System.Windows.Threading.DispatcherPriority.Normal);
      }).Start();
    }

    private void findUnusedPatchFiles()
    {
      ArrayList usedPatchFiles = new ArrayList();
      Type type = Type.GetTypeFromProgID("WindowsInstaller.Installer");
      WindowsInstaller.Installer installer = (WindowsInstaller.Installer)Activator.CreateInstance(type);

      foreach (string productCode in installer.Products)
      {
        StringList patches = installer.get_Patches(productCode);
        foreach (String patch in patches)
        {
          String patchFilePath = installer.get_PatchInfo(patch, "LocalPackage");
          usedPatchFiles.Add(patchFilePath);
        }
      }

      _unusedPatchFiles = new List<OrphanedItem>();
      IEnumerable<String> storedPatchFiles = Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Installer", "*.msp", SearchOption.TopDirectoryOnly);
      foreach (String storedPatchFile in storedPatchFiles)
      {
        if (!usedPatchFiles.Contains(storedPatchFile))
        {
          FileInfo patchFileInfo = new FileInfo(storedPatchFile);
          OrphanedItem item = new OrphanedItem(storedPatchFile);
          item.size = Math.Round((double)(patchFileInfo.Length / MEGABYTE), 2);
          item.filename = patchFileInfo.Name;
          _unusedPatchFiles.Add(item);
        }
      }
    }

    private void refreshOrphanedInfos()
    {
      double totalMbytes = 0;
      foreach (OrphanedItem item in _unusedPatchFiles)
      {
        totalMbytes += item.size;
      }
      tbOrphanedCount.Text = _unusedPatchFiles.Count.ToString();
      tbOrphanedTotalSize.Text = Math.Round(totalMbytes, 2).ToString();
      if (_unusedPatchFiles.Count == 0)
      {
        btnDeleteOrphaned.IsEnabled = false;
        btnMoveOrphaned.IsEnabled = false;
      }
      else
      {
        btnDeleteOrphaned.IsEnabled = true;
        btnMoveOrphaned.IsEnabled = true;
      }

      foreach(OrphanedItem item in _unusedPatchFiles)
      {
        item.percentageToTotal = Math.Round(item.size / totalMbytes, 2);
      }
    }

    private void startProcess(int action)
    {
        List<OrphanedItem> selectedItems = _unusedPatchFiles.Where(item => item.selected).ToList<OrphanedItem>();
        if (selectedItems.Count > 0)
        {
          String dstPath = null;
          String msg = null;
          _cancelProcess = false;

          btnCancel.IsEnabled = true;
          btnDeleteOrphaned.IsEnabled = false;
          btnMoveOrphaned.IsEnabled = false;
          btnScan.IsEnabled = false;
          orphanedList.IsEnabled = false;
          
          if (action == ACTION_MOVE)
          {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
              dstPath = dialog.FileName;
              if(!dstPath.EndsWith(@"\"))
              {
                dstPath += @"\";
              }
            }
            else
            {
              _cancelProcess = true;
            }
          }

          tbWait.Visibility = System.Windows.Visibility.Visible;
          int filecounter = 0;
          
          new Thread(delegate() {
            String desc = null;
            if (action == ACTION_DELETE)
            {
              desc = "Deleting file ";
            }
            else if (action == ACTION_MOVE)
            {
              desc = "Moving file ";
            }

            try
            {
              for (int i = 0; i < selectedItems.Count && !_cancelProcess; i++)
              {
                OrphanedItem item = selectedItems[i];
                msg = desc + item.filename;
                setProgress(msg, (i + 1), selectedItems.Count);
                
                if (action == ACTION_DELETE)
                {
                  removeReadonly(item.filepath);
                  File.Delete(item.filepath);
                }
                else if (action == ACTION_MOVE)
                {
                  String dstFile = dstPath + item.filename;
                  if (new FileInfo(dstFile).Exists)
                  {
                    removeReadonly(dstFile);
                    File.Delete(dstFile);
                  }
                  File.Move(item.filepath, dstFile);
                }

                _unusedPatchFiles.Remove(item);
                filecounter++;
              }
              msg = null;
            }
            catch (Exception ex)
            {
              MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
            }
            finally
            {
              if (msg == null)
              {
                if (action == ACTION_DELETE)
                {
                  msg = "Done! Deleted " + filecounter + " orphaned patch " + (filecounter == 1 ? "file." : "files.");
                }
                else if (action == ACTION_MOVE)
                {
                  msg = "Done! Moved " + filecounter + " orphaned patch " + (filecounter == 1 ? "file." : "files.");
                }
                setProgress(msg, 1, 1);
              }
              else
              {
                setProgress("", 0, 1);
              }

              this.Dispatcher.Invoke(delegate()
              {
                tbWait.Visibility = System.Windows.Visibility.Hidden;
                btnCancel.IsEnabled = false;
                btnScan.IsEnabled = true;
                orphanedList.IsEnabled = true;
                refreshOrphanedInfos();
                CollectionViewSource.GetDefaultView(orphanedList.ItemsSource).Refresh();
                orphanedList.Items.Refresh();
                if (_unusedPatchFiles.Count == 0)
                {
                  btnMoveOrphaned.IsEnabled = false;
                  btnDeleteOrphaned.IsEnabled = false;
                }
                else
                {
                  btnMoveOrphaned.IsEnabled = true;
                  btnDeleteOrphaned.IsEnabled = true;
                }

                if (cbAllOrphaned != null)
                {
                  cbAllOrphaned.IsChecked = false;
                }
              });
            }
          }).Start();
        }
        else
        {
          MessageBox.Show("No files selected");
        }
    }

    private void removeReadonly(String filepath)
    {
      FileAttributes fa = File.GetAttributes(filepath);
      bool isReadOnly = ((fa & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
      if (isReadOnly)
      {
        File.SetAttributes(filepath, fa & ~FileAttributes.ReadOnly);
      }
    }

    private void setProgress(String msg, int progress, int maximum)
    {
      this.Dispatcher.Invoke(delegate()
      {
        tbStatusMessage.Text = msg;
        pbStatusProgress.Value = progress;
        pbStatusProgress.Maximum = maximum;
      });
    }

    private void buttonClick(object sender, RoutedEventArgs e)
    {
      Button button = e.OriginalSource as Button;
      switch (button.Content.ToString())
      {
        case "Scan":
          init();
          setProgress("", 0, 1);
          break;
        case "Move":
          startProcess(ACTION_MOVE);
          break;
        case "Delete":
          startProcess(ACTION_DELETE);
          break;
        case "Cancel":
          _cancelProcess = true;
          break;
      }
    }

    private void cbAllOrphaned_Click(object sender, RoutedEventArgs e)
    {
      if (cbAllOrphaned == null)
      {
        cbAllOrphaned = e.OriginalSource as CheckBox;
      }

      foreach (OrphanedItem item in _unusedPatchFiles.Where(item => item.selected != cbAllOrphaned.IsChecked).ToList<OrphanedItem>())
      {
        item.selected = !item.selected;
      }
      orphanedList.Items.Refresh();
    }

    private void orphanedList_HeaderClick(object sender, RoutedEventArgs e)
    {
      GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
      ListSortDirection direction;

      if (headerClicked != null)
      {
        if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
        {
          if (headerClicked != _lastHeaderClicked)
          {
            direction = ListSortDirection.Ascending;
          }
          else
          {
            if (_lastDirection == ListSortDirection.Ascending)
            {
              direction = ListSortDirection.Descending;
            }
            else
            {
              direction = ListSortDirection.Ascending;
            }
          }

          string sortBy = null;
          string header = headerClicked.Column.Header as string;
          if(header.Equals("Size (MB)"))
          {
            sortBy = "size";
          }
          else if (header.Equals("File"))
          {
            sortBy = "filepath";
          } else if(header.Equals("Percentage"))
          {
            sortBy = "percentageToTotal";
          }

          ICollectionView dataView = CollectionViewSource.GetDefaultView(orphanedList.ItemsSource);
          if (dataView != null)
          {
            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
          }
        }
      }
    }
  }
}
