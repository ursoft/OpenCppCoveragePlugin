// OpenCppCoverage is an open source code coverage for C++.
// Copyright (C) 2014 OpenCppCoverage
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using EnvDTE;
using EnvDTE80;
using ICSharpCode.TreeView;
using OpenCppCoverage.VSPackage.CoverageRateBuilder;
using OpenCppCoverage.VSPackage.Helper;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace OpenCppCoverage.VSPackage.CoverageTree
{
    class CoverageTreeController: PropertyChangedNotifier
    {
        RootCoverageTreeNode rootNode;
        string filter;
        string warning;
        DTE2 dte;
        ICoverageViewManager coverageViewManager;

        readonly TreeNodeVisibilityManager visibilityManager;

        //-----------------------------------------------------------------------
        public readonly static string WarningMessage 
            = "Warning: Your program has exited with error code: ";

        //-----------------------------------------------------------------------
        public CoverageTreeController()
        {
            this.visibilityManager = new TreeNodeVisibilityManager();
        }

        //-----------------------------------------------------------------------
        public void UpdateCoverageRate(
            CoverageRate coverageRate,
            DTE2 dte,
            ICoverageViewManager coverageViewManager)
        {
            this.dte = dte;
            this.coverageViewManager = coverageViewManager;
            this.Root = new RootCoverageTreeNode(coverageRate);
            this.Filter = "";
            this.DisplayCoverage = true;

            if (coverageRate.ExitCode == 0)
                this.Warning = null;
            else
            {
                this.Warning = WarningMessage + coverageRate.ExitCode;
            }
        }

        //-----------------------------------------------------------------------
        public SharpTreeNode Current
        {
            set
            {
                var fileTreeNode = value as FileTreeNode;
                var fileCoverage = fileTreeNode?.Coverage;

                if (fileCoverage != null)
                {
                    if (this.dte == null)
                        throw new InvalidOperationException("UpdateCoverageRate should be call first.");
                    this.dte.ItemOperations.OpenFile(fileCoverage.Path, Constants.vsViewKindCode);
                }
            }
        }

        //-----------------------------------------------------------------------
        public RootCoverageTreeNode Root
        {
            get { return this.rootNode; }
            private set { this.SetField(ref this.rootNode, value); }
        }
        private UpdateUTBannersCommand updUT;
        public UpdateUTBannersCommand UpdUT
        {
            get
            {
                if (updUT == null)
                    updUT = new UpdateUTBannersCommand(this);

                return updUT;
            }
        }
        enum VisitOutcome { VO_ERROR, VO_UPDATED, VO_SKIPPED };
        string logFileName = Path.GetTempFileName();
        private VisitOutcome Visit(string fileName, int coveredLines, int totalLines) {
            string fl = "<not read yet>";
            try
            {
                string newBanner = $"//UT Coverage: {(int)(100.0 * coveredLines / totalLines + 0.5)}%, {coveredLines}/{totalLines}";
                using (var sr = File.OpenText(fileName)) { 
                    fl = sr.ReadLine();
                    if (fl.StartsWith(newBanner) || !fl.StartsWith("//UT Coverage") /*opc, websockets*/) return VisitOutcome.VO_SKIPPED;
                    if (coveredLines != totalLines)
                    {
                        newBanner = newBanner.Replace("100%", "99%");
                        newBanner += (fileName.EndsWith("_test.cpp")) ? ", ENOUGH" : ", NEED_MORE";
                    }
                    int pos = fl.IndexOf(" (");
                    if (pos != -1)
                        newBanner += fl.Substring(pos);
                    var remains = sr.ReadToEnd();
                    sr.Close();
                    File.WriteAllText(fileName, newBanner + "\r\n", Encoding.UTF8);
                    File.AppendAllText(fileName, remains);
                    return VisitOutcome.VO_UPDATED;
                }
            } catch(Exception ex)
            {
                File.AppendAllText(logFileName, $"Excepion {ex.ToString()} while processing {fileName} {coveredLines}/{totalLines}, fl={fl}");
                return VisitOutcome.VO_ERROR;
            }
        }
        public void UpdateUtBanners() {
            dte.Application.ExecuteCommand("File.SaveAll");
            int err = 0, upd = 0, skip = 0;
            foreach (var module in Root.Modules)
            {
                foreach (var child in module.Files)
                {
                    if (child.IsVisible && (child.Children == null || child.Children.Count == 0) && 
                        !child.Text.ToString().ToLower().EndsWith(".dll") &&
                        !child.Text.ToString().ToLower().EndsWith(".exe") && File.Exists(child.Text.ToString())) {
                        switch(Visit(child.Text.ToString(), child.CoveredLineCount, child.TotalLineCount)) { 
                            case VisitOutcome.VO_ERROR: err++; break;
                            case VisitOutcome.VO_UPDATED: upd++; break;
                            case VisitOutcome.VO_SKIPPED: skip++; break;
                        }
                    }
                }
            }
            if (err != 0) {
                System.Diagnostics.Process.Start("notepad.exe", logFileName);
                MessageBox.Show($"Files updated: {upd}, skipped: {skip}, err: {err}, total: {err + upd + skip}", "OpenCppCoverage", MessageBoxButton.OK, MessageBoxImage.Stop);
            } else { 
                MessageBox.Show($"Files updated: {upd}, skipped: {skip}, total: {err + upd + skip}", "OpenCppCoverage", MessageBoxButton.OK, MessageBoxImage.Information); 
            }
        }
        public class UpdateUTBannersCommand : ICommand
        {
            private CoverageTreeController parent;

            public UpdateUTBannersCommand(CoverageTreeController aParent)
            {
                parent = aParent;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                parent.UpdateUtBanners();
            }
        }

        //-----------------------------------------------------------------------
        public string Filter
        {
            get { return this.filter; }
            set 
            {
                if (SetField(ref this.filter, value))
                {
                    if (this.Root != null && value != null)
                    {
                        this.visibilityManager.UpdateVisibility(this.Root, value);
                        NotifyPropertyChanged("Root");
                    }
                }
            }
        }

        //-----------------------------------------------------------------------
        public string Warning
        {
            get { return this.warning; }
            set { SetField(ref this.warning, value); }
        }

        //-----------------------------------------------------------------------
        bool displayCoverage;
        public bool DisplayCoverage
        {
            get { return this.displayCoverage; }
            set
            {
                if (this.SetField(ref this.displayCoverage, value))
                    this.coverageViewManager.ShowCoverage = value;
            }
        }
    }
}
