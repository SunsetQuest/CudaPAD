// CudaPAD PTX/SASS viewer for NVidia's Cuda
// This projected is licensed under the terms of the MIT license.
// Copyright (c) 2009, 2013, 2014, 2015, 2016 Ryan S. White

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using DiffUtils;
using System.Collections;


namespace CudaPAD
{
    public partial class MainForm : Form
    {
        /// <summary>File type of source, CUDA or PTX</summary>
        string fileType;

        /// <summary>PTX target version</summary>
        string targetVersion;

        /// <summary>txtSrc.txt has some unsaved changes.</summary>
        bool unsavedChanges;

        /// <summary>A RegEx expression that cleans the txtSrc.txt of extra info.</summary>
        Regex regExCleaner;

        /// <summary>A snapshot of the cleaned txtSrc.txt. (last txtSrc key-press)</summary>
        string lastCleanedSrc;

        /// <summary>A snapshot of the cleaned txtSrc.txt that was compiled.</summary>
        string lastCompiledCleanedSrc;

        /// <summary>The most recent compiler output of PTX code.</summary>
        string lastOutput;

        /// <summary>Calculates the running time of the compiler. It is also used to
        /// check if the compiler is active at the current moment.</summary>
        Stopwatch compilerTimer = new Stopwatch();

        /// <summary>There were changes to txtSrc while the Cuda compiler was running.</summary>
        bool subsequentUpdateNeeded = false;

        /// <summary>Signals that after updating the destination window that the courser should be restored to location 0.</summary>
        bool setDestCurToDefaultLoc = true;

        /// <summary>Contains information for the code connecting lines.</summary>
        List<LineInfo> linesInfo = new List<LineInfo>();

        /// <summary>The last textbox selected. This is for copy and paste functions.</summary>
        ScintillaNET.Scintilla lastTextBoxSelected;

        /// <summary>Enables/Disables automatic recompiles. Usually disabled when the user does not want auto refresh enabled or has the right pane hidden.</summary>
        bool autoPtxCompileEnabled = true;

        /// <summary>Hides the left view. When disabled autoPTX and any lines should be disabled .</summary>
        bool ptxPaneEnabled = true;

        /// <summary>The location of visual studio command configuration file.(vcvarsall.bat)</summary>
        string VisualStudioVCVarsAllPath = "";

        /// <summary>The path to the temp directory for CudaPAD.</summary>
        string TEMP_PATH;

        /// <summary>The file name of the currently open file.(if any)</summary>
        string curOpenCudaFile = "";

        /// <summary>Specifies if before/after differencing is enabled.</summary>
        private bool _diffEnabled = true;

        /// <summary>Specifies if the code connector lines are enabled.</summary>
        private bool _linesEnabled = false;

        /// <summary>Calculates the running time of the compiler. It is also used to
        /// check if the compiler is active at the current moment.</summary>
        Timer changeTimer = new Timer();

        /// <summary>Contains a list of words or registers to highlight Key:Search_Word Val:Color.</summary>
        Dictionary<string, int> WordsToHighlight = new Dictionary<string, int>();

        Regex cleanupRegEx;

        public MainForm()
        {
            var vsPath = VS_Tools.GetVSPath(avoidPrereleases: true, requiredWorkload: "NativeDesktop");

            if (File.Exists(vsPath + @"\VC\bin\cl.exe") && File.Exists(vsPath + @"\VC\vcvarsall.bat")) //VS 2010,2013,2015
                VisualStudioVCVarsAllPath = vsPath + @"\VC\vcvarsall.bat";
            else if (File.Exists(vsPath + @"\VC\Auxiliary\Build\vcvars64.bat")) //VS 2017,2019
                VisualStudioVCVarsAllPath = vsPath + @"\VC\Auxiliary\Build\vcvars64.bat";
            else if (MessageBox.Show(this, "The cl.exe and//or vcvarsall.bat cannot be found in the Visual "
                    + "Studio directory. Please download Visual Studio from Microsoft's website. \nDo still "
                    + "wish to continue although CudaPAD may not run correctly?",
                    "Missing Cuda Compiler", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                Application.Exit();
            }

            /////////// SETUP TEMP\cupad09 FOLDER ///////////
            // Set All the Paths for CLEXE_PATH, and TEMP_PATH
            string system_temp_folder = System.Environment.GetEnvironmentVariable("TEMP");
            if (system_temp_folder == null)
                throw new SystemException("Environment Variable 'TEMP' does not exists.");

            // Make sure the folder cupad09 folder exists
            TEMP_PATH = AppDomain.CurrentDomain.BaseDirectory + @"cudaPadFiles_";
            if (!Directory.Exists(TEMP_PATH))
                Directory.CreateDirectory(TEMP_PATH);
            else //folder already exists
                foreach (string filename in new string[] {
                    "data.cubin",
                    "data.ptx",
                    "rtcof.dat",
                    "rtcof.bat",
                    "data.cu",
                    "info.txt",
                    "SASS.txt",
                    "data.cudafe1.c",
                    "data.cudafe1.stub.c",
                    "data.cudafe2.c",
                    "data.cudafe2.stub.c",
                    "data.cudafe1.cpp" ,
                    "data.cpp1.i" ,
                    "data.cpp1.i.res" ,
                    "data.cpp2.i" ,
                    "data.cpp2.i.res" ,
                    "data.cpp3.i" ,
                    "data.cpp3.i.res" ,
                    "data.cpp4.i" ,
                    "data.cpp4.i.res" ,
                    "data.cpp5.i" ,
                    "data.cpp5.i.res" ,
                    "data.cpp1.ii" ,
                    "data.cpp1.ii.res" ,
                    "data.cpp2.ii" ,
                    "data.cpp2.ii.res" ,
                    "data.cpp3.ii" ,
                    "data.cpp3.ii.res" ,
                    "data.cpp4.ii" ,
                    "data.cpp4.ii.res" ,
                    "data.cpp5.ii" ,
                    "data.cpp5.ii.res"
                })

                    if (File.Exists(TEMP_PATH + @"\" + filename))
                        File.Delete(TEMP_PATH + filename);


            // Initialize some random stuff
            InitializeComponent();
            Icon = Properties.Resources.PTXIcon48x48x8Only;
            SetPanelSize();
            cleanupRegEx = new Regex(
                // @"; *|"                              // remove ";"
                @"[\t ]*//<loop> .*\r\n|"               // remove unneeded comment
                + @"[\t ]*//.*__cudaparm.*(?=\r\n)|"    // remove unneeded comment
                + @" id:\d+|\+0(?=\])|"                 // remove unneeded id: comments
                + @"[\t ]*//[\t ]*(?=\r\n)|"            // remove empty "//" comments 
                + @"((?<=\r\n[A-Z]+\d+_\d+:)\r\n)|"     // remove return key after label (added 1/24/2016)
 //               + @"%|"                                 // remove "%" (added 1/17/2016)
                + @"[\t ]\.file[\t ]\d+[\t ]\"".*(?=\r\n)|" // remove 	.file	1 "C:\\....."
                + @"\r\n[\t ]*\r\n[\t ]*(?=\r\n)|"      // remove two blank lines in a row (added 1/24/2016)
                + @"__cudaparm_\w+(?=_\w+)|"            // shorten __cudaparam_
                + @"(?<=\$)Lt_\d+(?'tag'_\d+:?)(\r\n)?|"// shorten labels $Lt_0_22 --> $_22
                + @"\t// inline asm\r\n|"               // remove "// inline asm" comments (added 1/24/2016)
                + @"\t.loc[ \t]\d+[ \t].*\r\n"          // remove .loc 15 lines  (note: use spaces for sm_20 and higher)                
                , RegexOptions.Multiline | RegexOptions.Compiled);


            /////////// Setup RegExCleaner ///////////
            ConfigureRegExCleaner(false);

            linesDrawPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            txtDst.Text = "Loading....";

            lastTextBoxSelected = txtSrc;

            checkFileType();
            BuildNvccBatchFile();

            process.StartInfo.FileName = TEMP_PATH + @"\rtcof.bat";
            process.StartInfo.WorkingDirectory = TEMP_PATH;

            // Is there a file in the command line arguments load that
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                OpenFile(arg);
                if (!string.IsNullOrEmpty(curOpenCudaFile))
                    break;
            }

            txtDst.CurrentPos = 3;
            LinesEnabled = true;

            txtSrc.MouseWheel += delegate (object sender, MouseEventArgs e) { ReDrawLines(); };
            txtDst.MouseWheel += delegate (object sender, MouseEventArgs e) { ReDrawLines(); };

            changeTimer.Interval = 1000;
            changeTimer.Enabled = true;
            changeTimer.Tick += changeTimer_Tick;

            unsavedChanges = false;
        }


        private void ConfigureRegExCleaner(bool removeCarrageReturns)
        {
            const string RegExWithNewline = "("
            + @"""(?:\\.|[^\\""])*""|"  // match a double-quoted string
            + @"'(?:\\.|[^'\\])*'"      // match a single-quoted string
            + @"[^""'/]"                // stuff that couldn't begin one of the other alternatives.
            + "+)|"
            + @"/\*[^*]*\*+(?:[^/*][^*]*\*+)*/|" // match /*... */ a comment
            + @"//[^\n]*|"              // match a // comment
            + @"(?<=[\=\,\;\/\{\}\+\-\*\)\>\<]) |"// spaceAfterCharEx   
            + @" (?=[\=\,\;\/\{\}\+\-\*\)\>\<])|" // spaceBeforeCharEx   
            + @"[ \t](?=[ \t]+)|"       // if a space followed by another space
            + @"\t";                    // tabsEx

            const string RegExNoNewline = RegExWithNewline + @"|[\r\n][\r\n\t ]*"; // empty lines

            regExCleaner = new Regex(removeCarrageReturns ? RegExNoNewline : RegExWithNewline, RegexOptions.Compiled);
        }

        private void BuildNvccBatchFile()
        {
            if (this.fileType == "CUDA")
                BuildNvccBatchFile_CUDA();
            if (this.fileType == "PTX")
                BuildNvccBatchFile_PTX();
        }

        private void BuildNvccBatchFile_CUDA()
        {
            StringBuilder options = new StringBuilder();

            if (useFastMathToolStripMenuItem.Enabled)
                if (useFastMathToolStripMenuItem.Checked)
                    options.Append("--use_fast_math ");
            if (!defaultToolStripMenuItem.Checked)
                foreach (ToolStripMenuItem mi in optimizeToolStripMenuItem.DropDownItems)
                    if (mi.Checked)
                    {
                        options.Append(mi.Tag.ToString() + " ");
                        break;
                    }
            if (deviceDebugToolStripMenuItem.Enabled)
                if (deviceDebugToolStripMenuItem.Checked)
                    options.Append("--device-debug ");
            if (fTZFloatToZeroToolStripMenuItem.Enabled)
                options.Append("--ftz " + (fTZFloatToZeroToolStripMenuItem.Checked ? "true " : "false "));
            if (precDIVToolStripMenuItem.Enabled)
                options.Append("--prec-div " + (precDIVToolStripMenuItem.Checked ? "true " : "false "));
            if (precSqrtprecsqrtToolStripMenuItem.Enabled)
                options.Append("--prec-sqrt " + (precSqrtprecsqrtToolStripMenuItem.Checked ? "true " : "false "));
            if (fusedMultAddfmadToolStripMenuItem.Enabled)
                options.Append("--fmad " + (fusedMultAddfmadToolStripMenuItem.Checked ? "true " : "false "));
            if (relocatableDeviceCodeToolStripMenuItem.Enabled)
                options.Append("--relocatable-device-code " + (relocatableDeviceCodeToolStripMenuItem.Checked ? "true " : "false "));
            foreach (ToolStripMenuItem mi in architectureToolStripMenuItem.DropDownItems)
                if (mi.Checked)
                {
                    options.Append(mi.Tag.ToString() + " ");
                    break;
                }

            ///////// Create command line script file in temp folder ///////////
            using (StreamWriter sw = new StreamWriter(TEMP_PATH + @"\rtcof.bat"))
            {
                //to add other options we need to make this a full compile with a main
                sw.WriteLine("REM This script is...");
                sw.WriteLine("REM  - run on each change in the source code.");
                sw.WriteLine("REM  - over-written whenever any settings are modified in CudaPAD.");
                sw.WriteLine("");
                sw.WriteLine(@"call """ + VisualStudioVCVarsAllPath + @"""");
                sw.WriteLine("del data.cubin");
                sw.WriteLine(@"set path=%CUDA_PATH%\bin;%path%");
                sw.WriteLine(@"nvcc.exe -keep -cubin --generate-line-info -Xptxas=""-v"" "
                    + options + " data.cu  2>rtcof.dat >info.txt "); //-Xptxas=""-v"" shows reg usage
                sw.WriteLine(@"echo nvcc.exe -keep -cubin --generate-line-info -Xptxas=""-v"" "
                    + options + "data.cu  2>>rtcof.dat >>info.txt "); //-Xptxas=""-v"" shows reg usage
                sw.WriteLine("ren data.*.cubin data.cubin");
                if (cboOutType.Text == "SASS")
                    sw.WriteLine("cuobjdump -sass data.cubin > SASS.txt");
            }
        }

        private void BuildNvccBatchFile_PTX()
        {
            StringBuilder options = new StringBuilder();

            if (useFastMathToolStripMenuItem.Enabled)
                if (useFastMathToolStripMenuItem.Checked)
                    options.Append("--use_fast_math ");
            if (!defaultToolStripMenuItem.Checked)
                foreach (ToolStripMenuItem mi in optimizeToolStripMenuItem.DropDownItems)
                    if (mi.Checked)
                    {
                        options.Append("-Xptxas=\"" + mi.Tag.ToString() + "\" ");
                        break;
                    }
            if (deviceDebugToolStripMenuItem.Enabled)
                if (deviceDebugToolStripMenuItem.Checked)
                    options.Append("--device-debug ");
            if (fTZFloatToZeroToolStripMenuItem.Enabled)
                options.Append("--ftz " + (fTZFloatToZeroToolStripMenuItem.Checked ? "true " : "false "));
            if (precDIVToolStripMenuItem.Enabled)
                options.Append("--prec-div " + (precDIVToolStripMenuItem.Checked ? "true " : "false "));
            if (precSqrtprecsqrtToolStripMenuItem.Enabled)
                options.Append("--prec-sqrt " + (precSqrtprecsqrtToolStripMenuItem.Checked ? "true " : "false "));
            if (fusedMultAddfmadToolStripMenuItem.Enabled)
                options.Append("--fmad " + (fusedMultAddfmadToolStripMenuItem.Checked ? "true " : "false "));
            if (relocatableDeviceCodeToolStripMenuItem.Enabled)
                options.Append("--relocatable-device-code " + (relocatableDeviceCodeToolStripMenuItem.Checked ? "true " : "false "));
            options.Append("-arch " + targetVersion + " ");

            ///////// Create command line script file in temp folder ///////////
            using (StreamWriter sw = new StreamWriter(TEMP_PATH + @"\rtcof.bat"))
            {
                //to add other options we need to make this a full compile with a main
                sw.WriteLine("REM This script is...");
                sw.WriteLine("REM  - run on each change in the source code.");
                sw.WriteLine("REM  - over-written whenever any settings are modified in CudaPAD.");
                sw.WriteLine("");
                sw.WriteLine(@"call """ + VisualStudioVCVarsAllPath + @"""");
                sw.WriteLine("del data.cubin");
                sw.WriteLine(@"set path=%CUDA_PATH%\bin;%path%");
                sw.WriteLine(@"nvcc.exe -keep -cubin --generate-line-info -Xptxas=""-v"" "
                    + options + " data.ptx  2>rtcof.dat >info.txt "); //-Xptxas=""-v"" shows reg usage
                sw.WriteLine(@"echo nvcc.exe -keep -cubin --generate-line-info -Xptxas=""-v"" "
                    + options + " data.ptx  2>>rtcof.dat >>info.txt "); //-Xptxas=""-v"" shows reg usage
                sw.WriteLine("ren data.*.cubin data.cubin");
                if (cboOutType.Text == "SASS")
                    sw.WriteLine("cuobjdump -sass data.cubin > SASS.txt");
            }
        }

        /// <summary>
        /// Whenever the text is changed in the Cuda text window this triggers a countdown until the next compile. If another key is pressed in a short time the timer is reset.  This helps delay un-needed compiles.
        /// </summary>
        private void txtSrc_TextChanged(object sender, EventArgs e)
        {
            changeTimer.Stop();
            changeTimer.Start();
            unsavedChanges = true;

            checkFileType();
        }
        private void checkFileType()
        {
            if (Regex.Match(txtSrc.Text, @"\.version").Success)
            {
                txtSrc.ConfigurationManager.Language = "ptx";
                fileType = "PTX";
                targetVersion = Regex.Match(txtSrc.Text, @"\.target\s+(sm_\d+)").Groups[1].Value;
            }
            else
            {
                txtSrc.ConfigurationManager.Language = "cu";
                fileType = "CUDA";
            }
        }

        void changeTimer_Tick(object sender, EventArgs e)
        {
            changeTimer.Stop();

            string cleaned = regExCleaner.Replace(txtSrc.Text, "${1}");
            if (lastCleanedSrc == cleaned)
                return;

            // sometimes copy and paste will clear the screen; this prevents lastCleanedSrc from being set to that empty page
            if (cleaned.Length < 8)
                return;

            lastCleanedSrc = cleaned;

            if (autoPtxCompileEnabled)
                SaveSrcAndExec();
        }

        /// <summary>Saves the Cuda code(left panel) to a file and then triggers the compile timer.</summary>
        private void SaveSrcAndExec()
        {
            if (txtSrc.Text == "")
                return;
            if (this.fileType == "CUDA") 
                SaveSrcAndExec_CUDA();
            if (this.fileType == "PTX")
                SaveSrcAndExec_PTX();
        }

        private void SaveSrcAndExec_CUDA()
        {
            if (compilerTimer.IsRunning)
            {
                subsequentUpdateNeeded = true;
                return;
            }

            if (cboOutType.Text == "CODE")
            {
                txtDst.Text = lastCleanedSrc;
                return;
            }

            lastCompiledCleanedSrc = lastCleanedSrc;

            using (StreamWriter sw = new StreamWriter(TEMP_PATH + @"\data.cu"))
                sw.Write(txtSrc.Text);

            compilerTimer.Restart();

            process.Start();
            compileStatus.BackColor = Color.DarkOrange;
            compileStatus.Text = "Working";
            ClearLines();
        }

        private void SaveSrcAndExec_PTX()
        {
            if (compilerTimer.IsRunning)
            {
                subsequentUpdateNeeded = true;
                return;
            }

            if (cboOutType.Text == "CODE")
            {
                txtDst.Text = lastCleanedSrc;
                return;
            }

            lastCompiledCleanedSrc = lastCleanedSrc;

            using (StreamWriter sw = new StreamWriter(TEMP_PATH + @"\data.ptx"))
                sw.Write(txtSrc.Text);

            compilerTimer.Restart();

            process.Start();
            compileStatus.BackColor = Color.DarkOrange;
            compileStatus.Text = "Working";
            ClearLines();
        }

        /// <summary>Parses out the each error from cuda.out and writes it to the error list. It also filters out duplicates.</summary>
        public void AddItemToListLog(Match errorMatch)
        {
            string[] s = new string[3];
            s[0] = "";
            s[1] = errorMatch.Groups["line"].Value;
            s[2] = errorMatch.Groups["msg"].Value;

            ListViewItem listViewItem1 = new ListViewItem(s);
            if (errorMatch.Groups["error"].Value == "error")
                listViewItem1.ImageIndex = 1;
            else
                listViewItem1.ImageIndex = 0;

            // Set the text in the control
            if (listLog.InvokeRequired)
                listLog.Invoke((MethodInvoker)delegate { listLog.Items.Add(listViewItem1); });
            else
                listLog.Items.Add(listViewItem1);
        }

        /// <summary>Runs when nvcc.exe has finished compiling to cuda.out.</summary>
        private void BatchCompileProcess_Complete(object sender, EventArgs e)
        {
            //make sure PTX edit warning is removed
            lblPTXWarning.Visible = false;

            compilerTimer.Stop();
            txtCompileTime.Text = compilerTimer.ElapsedMilliseconds.ToString() + " ms";

            string logInfo = null;
            const int NumberOfRetries = 10;
            const int DelayOnRetry = 100;

            for (int i = 1; i <= NumberOfRetries; ++i) 
            {
                try 
                {
                    using (StreamReader sr = new StreamReader(TEMP_PATH + @"\info.txt"))
                        logInfo = sr.ReadToEnd();
                    break;
                }
                catch
                {
                    System.Threading.Thread.Sleep(DelayOnRetry);
                }
            }
            

            ///////// Lets update the compile info box /////////////
            Regex infoCleaner = new Regex(
                @"ptxas info[\s: \t]+(?<GMEM>\d+) bytes gmem(, (?<CONST>\d+) bytes cmem\[\d+\])?\r\n" +
                @"ptxas info[\s: \t]+Compiling entry function '\w+' for '\w+'\r\n" +
                @"ptxas info[\s: \t]+Function properties for \w+\r\n" +
                @"\s*(?<STACKFRAME>\d+) bytes stack frame, (?<SPILLSTORES>\d+) bytes spill stores, (?<SPILLLOADS>\d+) bytes spill loads\r\n" +
                @"ptxas info[\s: \t]+Used (?<REGS>\d+) registers(, (?<SMEM>\d+) bytes smem)?(, (?<CMEM0>\d+) bytes cmem\[\d+\])?(, (?<CMEM1>\d+) bytes cmem\[\d+\])?\r\n" +
                @"data.cu.*", RegexOptions.Compiled);
            Match m = infoCleaner.Match(logInfo);
            StringBuilder compileInfo = new StringBuilder();
            if (m.Success)
            {
                string cMem = m.Groups["CMEM0"].Success ? m.Groups["CMEM0"].Captures[0].Value : "N/A";
                if (m.Groups["CMEM1"].Success)
                    cMem += " + " + m.Groups["CMEM1"].Captures[0].Value;
                string sMem = m.Groups["SMEM"].Success ? m.Groups["SMEM"].Captures[0].Value : "N/A";
                string gMem = m.Groups["GMEM"].Success ? m.Groups["GMEM"].Captures[0].Value : "N/A";

                compileInfo.AppendLine("Memory-  Global: " + gMem + "\tConstant:    " + cMem);
                compileInfo.AppendLine("         Shared: " + sMem + "\tStack Frame: " + m.Groups["STACKFRAME"].Captures[0].Value);
                compileInfo.AppendLine("Regs-      Used: " + m.Groups["REGS"].Captures[0].Value);
                compileInfo.AppendLine("Spills-   Loads: " + m.Groups["SPILLLOADS"].Captures[0].Value + "\tStores:      " + m.Groups["SPILLSTORES"].Captures[0].Value);
            }

            // Show the output log in case the above fails.
            compileInfo.AppendLine("\r\n------- Full Output Log --------");
            compileInfo.AppendLine(Regex.Replace(logInfo, @"(ptxas\s*info\s*:\s*)|(Compiling entry function '\w+' for '\w+'\r\n)|data.cu\r\n", "", RegexOptions.Multiline));

            // Set the text in the control
            string compileText = compileInfo.ToString();
            if (txtCompileInfo.InvokeRequired)
                txtCompileInfo.Invoke((MethodInvoker)delegate { txtCompileInfo.Text = compileText; });
            else
                txtCompileInfo.Text = compileText;

            ///////////LETS UPDATE THE ERROR LIST LOG/////////////
            // read the file
            string logOut = null;
            for (int i = 1; i <= NumberOfRetries; ++i)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(TEMP_PATH + @"\rtcof.dat"))
                        logOut = sr.ReadToEnd();
                    break;
                }
                catch
                {
                    System.Threading.Thread.Sleep(DelayOnRetry);
                }
            }

            // add the items to the list
            Regex r = new Regex(@"(^.*data.cu\()(?<line>\d+)\): (?<error>[a-z]+): (?<msg>.+)\r\n", RegexOptions.Multiline);
            HashSet<string> dupDetect = new HashSet<string>();
            List<string[]> items = new List<string[]>();
            foreach (Match match in r.Matches(logOut))
            {
                string[] s = new string[3];
                s[0] = match.Groups["error"].Value;
                s[1] = match.Groups["line"].Value;
                s[2] = match.Groups["msg"].Value;

                //add only if it has not been added already.
                if (dupDetect.Add(s[1] + s[2]))
                    items.Add(s);
            }

            if (listLog.InvokeRequired)
                listLog.Invoke((MethodInvoker)delegate { UpdateListLogList(items); });
            else
                UpdateListLogList(items);            

            
            ///////////LETS UPDATE THE ERROR LIST LOG/////////////
            if (Regex.Match(logOut, "error:").Success)
            {
                toolStripDst.Invoke((MethodInvoker)delegate 
                {
                    compileStatus.BackColor = Color.Red;
                    compileStatus.Text = "Error";
                });
            }
            else
            {
                toolStripDst.Invoke((MethodInvoker)delegate
                {
                    compileStatus.BackColor = Color.DarkGreen;
                    compileStatus.Text = autoPtxCompileEnabled ? "Ready" : "Start";
                });

                txtDst.SuspendLayout();

                // Store cursor and scrollbars location
                int horizontalScrollOffset = txtDst.Scrolling.HorizontalScrollOffset;
                int cursorLoc = txtDst.CurrentPos;

                // If PTX is selected in the combobox then do the following...
                if (cboOutType.Text == "PTX")
                {
                    // First read in the PTX file output file from nvcc.exe
                    string ptxOutput = "";
                    for (int i = 1; i <= NumberOfRetries; ++i)
                    {
                        try
                        {
                            using (StreamReader sr = new StreamReader(TEMP_PATH + @"\data.ptx"))
                                ptxOutput = sr.ReadToEnd();
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(DelayOnRetry);
                        }
                    }
                   
                    // Remove and of the easy guide lines because we will need to re-draw them.
                    linesInfo.Clear();

                    // Lets split the PTX in two parts: Header and Body
                    int locOfStartOfBody;
                    if (sm10ToolStripMenuItem.Checked ||
                        sm11ToolStripMenuItem.Checked ||
                        sm13ToolStripMenuItem.Checked)
                    {
                        locOfStartOfBody = Regex.Match(ptxOutput, @"\t\.(entry|func).+\)\r\n\t\{",
                            RegexOptions.Singleline | RegexOptions.Compiled).Index;
                    }
                    else
                    { 
                        locOfStartOfBody = Regex.Match(ptxOutput, @"\.visible \.entry .+\)\r\n\{",
                            RegexOptions.Singleline | RegexOptions.Compiled).Index;
                    }
                    string header = ptxOutput.Substring(0, locOfStartOfBody);
                    string ptx_body = ptxOutput.Substring(locOfStartOfBody);

                    // Lets check to see if there is no code (maybe there is a "struct" but no functions)
                    if (string.IsNullOrEmpty(header)) //b/c of side effects the header would be in the body if this happens - so the header would be empty.
                    {
                        header = ptx_body;
                        ptx_body = "";
                    }

                    // Extract the fileNumberOfCudaFileInPTX. We get this information from the ".file" line.
                    string fileNumberOfCudaFileInPTX = Regex.Match(ptxOutput, @".*\t\.file\t(?<num>\d+)\s.+\\data.cu"".*", 
                        RegexOptions.Singleline | RegexOptions.Compiled).Groups["num"].Value;
                    if (string.IsNullOrEmpty(fileNumberOfCudaFileInPTX))
                        fileNumberOfCudaFileInPTX = "xi045nn"; //something that will never be found

                    // move the ".loc" source code line to the next line in the format "...// Line: __ ".
                    string regex_line = @"\t.loc\s" + fileNumberOfCudaFileInPTX + @"\s(?'line'\d+)\s\d+\r\n(?'nextline'.+)\r\n";
                    ptx_body = Regex.Replace(ptx_body, regex_line, "${nextline}	// Line: ${line}\r\n", 
                        RegexOptions.Multiline | RegexOptions.Compiled);

                    // Sometimes when there is an error the ptx_body will be empty. To prevent errors
                    // in CudaPad a ".entry _f {$f: exit}" is filled in the body. (an empty kernel)
                    if (string.IsNullOrEmpty(ptx_body)) ptx_body = ".entry _f {$f: exit}";

                    // Let cleanup the PTX text by removing unneeded comments, unneeded ids, empty "//"
                    // return key after label, remove "%", remove two blank lines in a row, shorten
                    // __cudaparam_, shorten labels, remove "// inline asm" comments, remove .loc 15 lines.
                    // Note: see the implementation of cleanupRegEx above to see what it removes.
                    ptx_body = cleanupRegEx.Replace(ptx_body, "${tag}");

                    // Lets remove the line numbers and register numbers and then do a 'diff' compare. (updated 1/24/2016)
                    string toCompare = Regex.Replace(ptx_body, @"(?<=[rfp][hl]?)\d+|(?<=// Line: )\d+|(?<=\$_)\d+", "__",RegexOptions.Compiled);
                    ptx_body = DiffCalc(toCompare, ptx_body);

                    // Now lets gather some information on the ptx_body like SourceLineNumber, ptxLineNumber, ect. 
                    // Lets do this one line at a time. 
                    string[] ptxLines = Regex.Split(ptx_body, "\n",RegexOptions.Compiled);
                    for (int ptxLineNumber = 0; ptxLineNumber < ptxLines.Count(); ptxLineNumber++)
                    {
                        string line = ptxLines[ptxLineNumber];

                        string srcLineNo = Regex.Replace(line, @".*// Line: (?'tag'\d+)|.*", "${tag}", RegexOptions.Compiled);

                        linesInfo.Add(new LineInfo() 
                        { 
                            lineNo = ptxLineNumber + 1, 
                            srcLineNo = (string.IsNullOrEmpty(srcLineNo)) ? -1 : int.Parse(srcLineNo), 
                            regRead1 = "", 
                            regRead1IsLast = false, 
                            regRead2 = "", 
                            regRead2IsLast = false, 
                            regWritten = "", 
                            regWrittenIsNew = false 
                        });
                    }

                    // More Cleanup, lets remove empty "// " AND "//Line: 00" AND "Line: __"
                    ptx_body = Regex.Replace(ptx_body, @"\s*//( Line:( \d+| __)|\s*(?=(\r\n)|//))", "",RegexOptions.Multiline);;

                    // We are done, lets write the new ptx_body to the right side window
                    txtDst.Invoke((MethodInvoker)delegate { txtDst.Text = ptx_body; });

                    // Lets also draw the guide lines
                    ReDrawLines();
                }
                else if (cboOutType.Text == "SASS")
                {
                    // Extract info from SASS file
                    string SASS_body;

                    try 
                    {
                        using (StreamReader SASSReader = new StreamReader(TEMP_PATH + @"\SASS.txt"))
                            SASS_body = SASSReader.ReadToEnd();
                    }
                    catch (Exception)
                    {
                        SASS_body = "";
                    }
                    const string regex = @"(?<header1>\n\tcode for sm_\d+)|(?<header2>\s+Function\s+:\s+\w+)|(?<header3>\s+\.headerflags\s+.+)|(?<SpaceBeforeLineNum>(?<=\n)\s{8})|(?<SpaceAfterLineNum>(?<=\/)\ {10})|(?<CombineEmptyLines>(?<=[0-9a-hA-H])\s\*\/\s{15,}\/\*(?=\s[0-9a-hA-H]))";
                    SASS_body = Regex.Replace(SASS_body, regex, "", RegexOptions.Compiled);
                   
                    // separate multiple columns into a single column
                    string toCompare = Regex.Replace(SASS_body, @"R\d+|/\*(0x)?[0-9a-fA-F]+\*/", "__", RegexOptions.Compiled);
                    SASS_body = DiffCalc(toCompare, SASS_body);
                    txtDst.Invoke((MethodInvoker)delegate { txtDst.Text = SASS_body; });
                }

                // Restore cursor and scrollbars location
                if (setDestCurToDefaultLoc)
                {
                    int locOfEntry = 0;
                    setDestCurToDefaultLoc = false;
                    if (cboOutType.Text == "PTX")
                        locOfEntry = txtDst.Text.LastIndexOf(".entry");
                    txtDst.Invoke((MethodInvoker)delegate
                    {
                        // Make sure locOfEntry > 0 and then set Caret position.
                        txtDst.CurrentPos = Math.Max(0, locOfEntry); 
                        txtDst.Scrolling.ScrollToCaret();
                    });
                }
                else // Restore cursor and scrollbars location
                {
                    // Set the text in the control
                    txtDst.Invoke((MethodInvoker)delegate { 
                        txtDst.CurrentPos = cursorLoc;
                        txtDst.Scrolling.HorizontalScrollOffset = horizontalScrollOffset;
                    });
                }
                txtDst.Invalidate();
                txtDst.ResumeLayout();
            }

            if (subsequentUpdateNeeded)
            {
                subsequentUpdateNeeded = false;
                if (lastCompiledCleanedSrc != regExCleaner.Replace(txtSrc.Text, "${1}"))
                    SaveSrcAndExec();
            }

        }

        private void UpdateListLogList(List<string[]> msgs)
        {
            listLog.SuspendLayout();
            listLog.Items.Clear();

            foreach (string[] msg in msgs)
            {
                ListViewItem listViewItem1 = new ListViewItem(msg);
                listViewItem1.ImageIndex = (msg[0] == "error") ? 1 : 0;

                // Subtract 1 because we want 1 based (not 0 based)
                string lineNo = listViewItem1.SubItems[1].Text;
                int lineNoInt;
                if (int.TryParse(lineNo, out lineNoInt))
                    if (lineNoInt > 0)
                        listViewItem1.SubItems[1].Text = (lineNoInt - 1).ToString();

                listLog.Items.Add(listViewItem1);
            }

            // Now sort the columns
            ListViewItemColumnSorter columnSorter = new ListViewItemColumnSorter();
            listLog.ListViewItemSorter = columnSorter;
            columnSorter.int_mode = true;
            columnSorter.curColumn = listLog.Columns[1].Index; // first sort by line #
            listLog.Sort();
            columnSorter.int_mode = false;
            columnSorter.curColumn = listLog.Columns[0].Index; // then sort by type
            listLog.Sort();
            listLog.ListViewItemSorter = null;
            listLog.ResumeLayout();
        }


        private void cboOutType_SelectedIndexChanged(object sender, EventArgs e)
        {
            BuildNvccBatchFile();
            lastOutput = null;
            setDestCurToDefaultLoc = true;
            linesInfo.Clear();

            switch (cboOutType.Text)
            {
                case "CODE":
                    toolStripBtnLines.Enabled = false;
                    txtDst.LineWrapping.Mode = ScintillaNET.LineWrappingMode.Word;
                    break;

                case "SASS":
                    toolStripBtnLines.Enabled = false;
                    txtDst.LineWrapping.Mode = ScintillaNET.LineWrappingMode.None;
                    txtDst.ConfigurationManager.Language = "sass";
                    ConfigureRegExCleaner(!LinesEnabled);
                    break;

                case "PTX":
                    toolStripBtnLines.Enabled = true;
                    txtDst.LineWrapping.Mode = ScintillaNET.LineWrappingMode.None;
                    txtDst.ConfigurationManager.Language = "ptx";
                    ConfigureRegExCleaner(true);
                    break;
            }

            SaveSrcAndExec();
            ReDrawLines();
        }


        /// <summary>
        /// Compares two different strings and then marks the rows that changed using comments.
        /// </summary>
        private string DiffCalc(string toCompare, string toDisplay)
        {
            if (!_diffEnabled)
                return toDisplay;

            StringBuilder ret = new StringBuilder(toDisplay.Length * 2);

            if (lastOutput == null)
            {
                lastOutput = toCompare;
                return toDisplay;
            }

            Diff.Item[] f = Diff.DiffText(lastOutput, toCompare, true, true, false);
            string[] aLines = lastOutput.Split('\n');
            string[] bLines = toDisplay.Split('\n');

            int n = 0;
            for (int fdx = 0; fdx < f.Length; fdx++)
            {
                Diff.Item aItem = f[fdx];

                // write unchanged lines
                while ((n < aItem.StartB) && (n < bLines.Length))
                {
                    ret.AppendLine(bLines[n].TrimEnd('\r'));
                    n++;
                }

                // write inserted lines
                while (n < aItem.StartB + aItem.insertedB)
                {
                    ret.AppendLine(@"/*new*/" + bLines[n].TrimEnd('\r'));
                    n++;
                }

                // write deleted lines
                for (int m = 0; m < aItem.deletedA; m++)
                    ret.AppendLine(@"//del//" + aLines[aItem.StartA + m].TrimEnd('\r'));
            }

            // write rest of unchanged lines
            while (n < bLines.Length)
            {
                ret.AppendLine(bLines[n].TrimEnd('\r'));
                n++;
            }

            lastOutput = toCompare;
            return ret.ToString();
        }


        /// <summary>Clears all the visual code connector lines.</summary>
        private void ClearLines()
        {
            GraphicsPath path = new GraphicsPath(new Point[] { new Point { X = 0, Y = 0 } }, new Byte[] { 0 });
            linesDrawPanel.Region = new Region(path);
            path.Dispose();
        }


        /// <summary>Draws visual code connecting lines that connect the Cuda code to PTX code. These are a visual aid that help in understanding PTX.</summary>
        private void ReDrawLines()
        {
            if (!LinesEnabled)
                return;
             
            txtSrc.Refresh();
            txtDst.Refresh();

            if (linesInfo.Count > 0)
            {
                GraphicsPath path = new GraphicsPath(FillMode.Winding);
                byte[] types = { (byte)PathPointType.Start, 3, 3, 3, 1, 3, 3, 3, 1 };
                int txtSrcRowHeight = txtSrc.Lines.FirstVisible.Height;
                int txtSrcfirstVisible = txtSrc.Lines.FirstVisible.Number;
                int txtSrcVisibleLineCt = txtSrc.Lines.VisibleCount;
                int txtSrcOverExtendPixels = 4;
                float txtSrcFontSz = (txtSrc.Font.Size + (float)txtSrc.ZoomFactor) * 0.75f ;

                int txtDstRowHeight = txtDst.Lines.FirstVisible.Height;
                int txtDstfirstVisible = txtDst.Lines.FirstVisible.Number;
                int txtDstVisibleLineCt = txtDst.Lines.VisibleCount;
                int txtDstOverExtendPixels = 4;

                foreach (LineInfo arrowInfo in linesInfo)
                    if (arrowInfo.srcLineNo > 0)
                    {
                        const int width = 1;
                        int s_x = (int)(txtSrc.Lines[arrowInfo.srcLineNo - 1].Length * (txtSrcFontSz+1)); // get source-x width
                        s_x = Math.Min(s_x, txtSrc.Width-15); // make sure it does not start past the end of the screen
                        int s_y = (int)((arrowInfo.srcLineNo - txtSrcfirstVisible) * txtSrcRowHeight) - (txtSrcRowHeight / 2);
                        int d_x = txtSrc.Width + 60;
                        int d_y = (int)((arrowInfo.lineNo - txtDstfirstVisible) * txtDstRowHeight) - (txtDstRowHeight / 2);

                        if (s_y < (0 - txtSrcOverExtendPixels) * txtSrcRowHeight) 
                            continue;
                        if (s_y > (0 + txtSrcVisibleLineCt + txtSrcOverExtendPixels) * txtSrcRowHeight)
                            continue;
                        if (d_y < (0 - txtDstOverExtendPixels) * txtDstRowHeight) 
                            continue;
                        if (d_y > (0 + txtDstVisibleLineCt + txtDstOverExtendPixels) * txtDstRowHeight) 
                            continue;

                        Point[] pts2 = { new Point(s_x, s_y), new Point(s_x + 60, s_y), new Point(d_x - 60, d_y), new Point(d_x, d_y), new Point(d_x, d_y + width), new Point(d_x - 60, d_y + width), new Point(s_x + 60, s_y + width), new Point(s_x, s_y + width), new Point(s_x, s_y) };
                        GraphicsPath subpath = new GraphicsPath(pts2, types);
                        path.AddPath(subpath, false);
                    }
                linesDrawPanel.Region = new Region(path);
                path.Dispose();
            }
            linesDrawPanel.Visible = true; // added 1-1-2015
        }


        /// <summary>
        /// Enables or Disables the visual code connecting lines.
        /// </summary>
        public bool LinesEnabled
        {
            get
            {
                return _linesEnabled;
            }
            set
            {
                _linesEnabled = value;
                if (_linesEnabled) // setting to enabled
                {
                    toolStripBtnLines.Image = Properties.Resources.lines24x22On;
                    // first clear the plane so that it does not show anything until RedrawLines Gets back.
                    ClearLines();
                    // second: Start a ReDraw
                    ReDrawLines();
                    ConfigureRegExCleaner(false);
                    linesDrawPanel.Visible = true;
                }
                else // disabling
                {
                    toolStripBtnLines.Image = Properties.Resources.lines24x22Off;
                    linesDrawPanel.Visible = false;
                    ConfigureRegExCleaner(true);
                }
            }
        }

        bool _linesEnabledLast = false;
        private void toolStripBtnLines_EnabledChanged(object sender, EventArgs e)
        {
            if (toolStripBtnLines.Enabled)  // it has been re-enabled
                LinesEnabled = _linesEnabledLast;
            else // it has been disabled
            {
                _linesEnabledLast = LinesEnabled;
                LinesEnabled = false;
            }
        }

        private void listLog_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListViewItemColumnSorter columnSorter = new ListViewItemColumnSorter();
            listLog.ListViewItemSorter = columnSorter;
            columnSorter.curColumn = e.Column;
            listLog.Sort();
            listLog.ListViewItemSorter = null;
        }

        private void listLog_MouseClick(object sender, MouseEventArgs e)
        {
            if (MouseButtons.Left == e.Button)
            {
                int line;
                if (listLog.FocusedItem != null)
                    if (int.TryParse(listLog.FocusedItem.SubItems[1].Text, out line))
                    {
                        txtSrc.Focus();
                        txtSrc.GoTo.Line(line);
                    }
            }
        }

        private void txtDst_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if ((e.KeyValue > 0x20) && (e.KeyValue < 0xC0))
                lblPTXWarning.Visible = true;
        }

        private bool _prevLinesEnabledState;
        private bool _prevAutoCompileState;
        private void toolMenuExpandRight_Click(object sender, EventArgs e)
        {
            ptxPaneEnabled = !ptxPaneEnabled;
            if (ptxPaneEnabled)
            { // show PTX side panel
                autoPtxCompileEnabled = _prevAutoCompileState;
                LinesEnabled = _prevLinesEnabledState;
                splitContainer1.Panel2Collapsed = false;
                toolMenuExpandRight.Text = ">>";
            }
            else
            { // hide PTX side panel
                _prevLinesEnabledState = LinesEnabled;
                _prevAutoCompileState = autoPtxCompileEnabled;

                LinesEnabled = false;
                autoPtxCompileEnabled = false;

                splitContainer1.Panel2Collapsed = true;
                toolMenuExpandRight.Text = "<<";
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CheckForUnsavedChanges())
                e.Cancel = true;
        }

        private void findToolStripMenuItemFind_Click(object sender, EventArgs e)
        {
            if (txtDst.Focused)
                txtDst.FindReplace.ShowFind();
            else
                txtSrc.FindReplace.ShowFind();
        }

        ////////////////////////////////////////////////////////////////////////////
        //////////////////////////  File Operations ////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////

        private void OpenFile(string arg)
        {
            if (!arg.ToLower().EndsWith(".exe"))
                if (File.Exists(arg))
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader(arg))
                        {
                            txtSrc.Text = "";
                            txtSrc.AppendText(sr.ReadToEnd());
                        }
                        curOpenCudaFile = arg;
                        TEMP_PATH = Path.GetDirectoryName(arg) + @"\cudaPadFiles_" + Path.GetFileNameWithoutExtension(arg); 
                        Directory.CreateDirectory(TEMP_PATH);
                        checkFileType();
                        BuildNvccBatchFile();
                        process.StartInfo.FileName = TEMP_PATH + @"\rtcof.bat";
                        process.StartInfo.WorkingDirectory = TEMP_PATH;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("The file could not be read: " + e.Message);
                    }

                }
        }

        /// <summary>
        /// Checks to see if the file needs saving and if it does then it prompts user.
        /// </summary>
        /// <returns>Returns true if cancel was clicked.</returns>
        private bool CheckForUnsavedChanges()
        {
            if (unsavedChanges)
                switch (MessageBox.Show("Unsaved changes - save them now?", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            saveAsToolStripMenuItem_Click(this, null);
                            return false;
                        }
                        break;
                    case DialogResult.No:
                        return false;
                }
            else
                return false;
            return true;
        }

        private void useFastMathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool useFastMath = useFastMathToolStripMenuItem.Checked;

            fTZFloatToZeroToolStripMenuItem.Enabled = !useFastMath;
            precDIVToolStripMenuItem.Enabled = !useFastMath;
            precSqrtprecsqrtToolStripMenuItem.Enabled = !useFastMath;
            fusedMultAddfmadToolStripMenuItem.Enabled = !useFastMath;

            // The below sets adds checks in front of the disabled checkboxes.
            //if (useFastMath)
            //{
            //    fTZFloatToZeroToolStripMenuItem.Checked = true;
            //    precDIVToolStripMenuItem.Checked = false;
            //    precSqrtprecsqrtToolStripMenuItem.Checked = false;
            //    fusedMultAddfmadToolStripMenuItem.Checked = true;
            //}

            toolStripMenuItem_Click(this, null);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(curOpenCudaFile))  // ""= no name for the current file
            {
                // try to find a name for the file using the first _global_ function name
                Match m = Regex.Match(txtSrc.Text, @"__global__\s+\w+\s+(\w+)\(");
                saveFileDialog1.FileName = m.Success ? m.Groups[1].Value + ".cu" : "MyCudaFile.cu";
            }
            else
                saveFileDialog1.FileName = curOpenCudaFile;

            if (DialogResult.OK == saveFileDialog1.ShowDialog())
            {
                StreamWriter sw = new StreamWriter(saveFileDialog1.FileName);
                sw.Write(txtSrc.Text);
                sw.Flush(); sw.Close();
                unsavedChanges = false;
                curOpenCudaFile = saveFileDialog1.FileName;
                Text = "CudaPAD - " + Path.GetFileName(curOpenCudaFile);
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        ///////////////////////////  Printing Stuff ////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (txtDst.Focused)
                txtDst.Printing.PrintPreview();
            else
                txtSrc.Printing.PrintPreview();
        }
        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (txtDst.Focused)
                txtDst.Printing.Print();
            else
                txtSrc.Printing.Print();
        }

        private void pageSetupToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (txtDst.Focused)
                txtDst.Printing.ShowPageSetupDialog();
            else
                txtSrc.Printing.ShowPageSetupDialog();
        }

        ////////////////////////////////////////////////////////////////////////////
        ///////////////////////  Simple Text Box events ////////////////////////////
        ////////////////////////////////////////////////////////////////////////////

        private void txtSrc_SelectionChanged(object sender, EventArgs e)
        {
            // Update the line number on the bottom status bar
            tsbLineNu.Text = txtSrc.Lines.Current.Number.ToString();
            //(txtSrc.GetLineFromCharIndex(txtSrc.SelectionStart) + 1).ToString(); 
        }

        private void ReDrawLines(object sender, MouseEventArgs e)
        {
            ReDrawLines();
        }

        private void ReDrawLines(object sender, ScrollEventArgs e)
        {
            ReDrawLines();
        }

        private void splitContainer1_Resize(object sender, EventArgs e)
        {
            SetPanelSize();
            ReDrawLines();
        }

        private void SetPanelSize()
        {
            linesDrawPanel.Width = splitContainer2.Panel1.Width + 90;
            linesDrawPanel.Height = splitContainer2.Panel1.Height - 30;
        }

        private void ToggleLinesEnabled(object sender, EventArgs e)
        {
            LinesEnabled = !LinesEnabled;
        }

        private void ToggleDiffEnabled(object sender, EventArgs e)
        {
            _diffEnabled = !_diffEnabled;
            if (_diffEnabled)
                toolStripBtnDiff.Image = Properties.Resources.Diff24x22On;
            else
                toolStripBtnDiff.Image = Properties.Resources.Diff24x22Off;
            SaveSrcAndExec();
        }

        private void txtDst_Enter(object sender, EventArgs e)
        {
            lastTextBoxSelected = txtDst;
        }

        private void txtSrc_Enter(object sender, EventArgs e)
        {
            lastTextBoxSelected = txtSrc;
        }

        private void txtCompileInfo_Enter(object sender, EventArgs e)
        {
            lastTextBoxSelected = txtCompileInfo;
        }

        private void txtSrc_ZoomChanged(object sender, EventArgs e)
        {
            txtSrc.Refresh();
            ReDrawLines();
        }

        private void txtDst_ZoomChanged(object sender, EventArgs e)
        {
            txtSrc.Refresh();
            ReDrawLines();
        }

        private void txtSrc_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            linesDrawPanel.Visible = false;
        }

        private void ReDrawLines(object sender, EventArgs e)
        {
            ReDrawLines();
        }

        private void compileStatus_Click(object sender, EventArgs e)
        {
            SaveSrcAndExec();
        }

        ////////////////////////////////////////////////////////////////////////////
        ///////////////////////////  ToolStrip Stuff ///////////////////////////////
        ////////////////////////////////////////////////////////////////////////////

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckForUnsavedChanges())
            {
                txtSrc.Text = "extern \"C\" __global__ void myFunction(const float * input, float * output)\n{\n\t\n}";
                txtSrc.CurrentPos = 78;
                unsavedChanges = false;
                lastOutput = txtSrc.Text;
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CheckForUnsavedChanges()) return;

            if (DialogResult.OK == openFileDialog1.ShowDialog())
                OpenFile(openFileDialog1.FileName);

            this.txtSrc.TextChanged += new System.EventHandler(this.txtSrc_TextChanged);
            this.txtDst.Text = "";
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.UndoRedo.Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.UndoRedo.Redo();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.Clipboard.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.Clipboard.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.Clipboard.Paste();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lastTextBoxSelected.Selection.SelectAll();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox aboutBox = new AboutBox();
            aboutBox.ShowDialog(this);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ToolStripMenuItemLogList_copy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(listLog.FocusedItem.SubItems[2].Text);
        }

        private void ToolStripMenuItemLogList_google_Click(object sender, EventArgs e)
        {
            string rx = @"(?!"";""|""add more here"")""(\w|;)+""";
            string toSearch = "%22" + Regex.Replace(listLog.FocusedItem.SubItems[2].Text, rx, "%22+%22") + "%22";
            Process.Start("http://www.google.com/search?complete=1&hl=en&q=cuda+" + toSearch);
        }

        private void toolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.OwnerItem.Name)
            {
                case "architectureToolStripMenuItem":
                    foreach (ToolStripMenuItem mi in architectureToolStripMenuItem.DropDownItems)
                        mi.Checked = ((string)mi.Tag == (string)e.ClickedItem.Tag);
                    break;

                case "optimizeToolStripMenuItem":
                    foreach (ToolStripMenuItem mi in optimizeToolStripMenuItem.DropDownItems)
                        mi.Checked = ((string)mi.Tag == (string)e.ClickedItem.Tag);
                    break;

                case "useFastMathToolStripMenuItem":
                    useFastMathToolStripMenuItem.Checked = !useFastMathToolStripMenuItem.Checked;
                    break;

                default:
                    throw new MissingMethodException("Error applying selected option.");
            }

            BuildNvccBatchFile();
            SaveSrcAndExec();
        }

        private void toolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuildNvccBatchFile();
            SaveSrcAndExec();
        }

        private void toolStripBtnAuto_Click(object sender, EventArgs e)
        {
            autoPtxCompileEnabled = !autoPtxCompileEnabled;
            toolStripBtnAuto.Image = autoPtxCompileEnabled ?
                Properties.Resources.auto24x22On : Properties.Resources.auto24x22Off;
            compileStatus.Text = autoPtxCompileEnabled ? "Ready" : "Start";
        }

        private void ToolStripMenuItemOpenGitHubSite_Click(object sender, EventArgs e)
        {
            Process.Start(@"https://github.com/SunsetQuest/CudaPAD");
        }

        private void ToolStripMenuItemOpenCodeProjectSite_Click(object sender, EventArgs e)
        {
            Process.Start(@"http://www.codeproject.com/Articles/999744/CudaPAD");
        }

        private void ToolStripMenuItemOpenTEMPPath_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", TEMP_PATH);
        }

        private void openNvccexeBatchScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("notepad.exe", TEMP_PATH + @"\rtcof.bat");
        }

        private void txtDst_MouseDown(object sender, MouseEventArgs e)
        {
            KnownColor[] COLORS = new KnownColor[]
            {
                KnownColor.Blue,
                KnownColor.Red,
                KnownColor.Green,
                KnownColor.Purple,
                KnownColor.Gray,
                KnownColor.CadetBlue,
                KnownColor.Crimson,
                KnownColor.DarkBlue,
                KnownColor.Brown,
                KnownColor.DarkCyan,
                KnownColor.DarkGreen,
                KnownColor.DarkMagenta,
                KnownColor.DarkSlateBlue,
                KnownColor.ForestGreen,
                KnownColor.Goldenrod,
                KnownColor.Green,
                KnownColor.Indigo,
                KnownColor.Maroon,
                KnownColor.MediumBlue,
                KnownColor.MediumAquamarine,
                KnownColor.MediumOrchid,
                KnownColor.MediumPurple,
                KnownColor.MediumSlateBlue,
                KnownColor.MediumVioletRed,
                KnownColor.Olive,
                KnownColor.OliveDrab,
                KnownColor.Peru,
                KnownColor.RoyalBlue,
                KnownColor.SaddleBrown,
                KnownColor.SlateGray,
                KnownColor.Teal,
                KnownColor.Tomato,
                KnownColor.YellowGreen,
            };

            string curWord = txtDst.GetWordFromPosition(txtDst.CurrentPos);

            if (string.IsNullOrWhiteSpace(curWord))
                return;

            if (WordsToHighlight.ContainsKey(curWord))
            {
                txtDst.GetRange().ClearIndicator( WordsToHighlight[curWord] );
                WordsToHighlight.Remove(curWord);
            }
            else
            {
                // find a color
                int colorToUse = 0;
                BitArray used = new BitArray(32);
                foreach (var r in WordsToHighlight)
                    used[r.Value] = true;
                for (int i = 0; i < 32; i++)
                    if (!used[i])
                    {
                        colorToUse = i;
                        break;
                    }

                // add entry
                WordsToHighlight.Add(curWord, colorToUse);

                // add to screen
                txtDst.GetRange().ClearIndicator(colorToUse);

                // Update indicator appearance
                txtDst.Indicators[colorToUse].Style = ScintillaNET.IndicatorStyle.StraightBox;
                txtDst.Indicators[colorToUse].OutlineAlpha = 100;
                txtDst.Indicators[colorToUse].Alpha = 50;
                txtDst.Indicators[colorToUse].Color = Color.FromKnownColor(COLORS[colorToUse]);
                
                //txtDst.GetRange(100, 120).SetIndicator(NUM);

                IList <ScintillaNET.Range> ranges = txtDst.FindReplace.FindAll(curWord, ScintillaNET.SearchFlags.WholeWord);
                foreach (ScintillaNET.Range range in ranges)
                    range.SetIndicator(colorToUse);
            }          
        }
    }

    struct LineInfo
    {
        public int lineNo;
        public int srcLineNo;
        public string regWritten;
        public string regRead1;
        public string regRead2;
        public bool regWrittenIsNew;
        public bool regRead1IsLast;
        public bool regRead2IsLast;
    }

    public class ListViewItemColumnSorter : IComparer
    {
        public int curColumn = 0;
        public bool int_mode = false;
        public int Compare(object x, object y)
        {
            string a = ((ListViewItem)x).SubItems[curColumn].Text;
            string b = ((ListViewItem)y).SubItems[curColumn].Text;
            if (int_mode)
                return int.Parse(a) - int.Parse(b);
            else
                return string.Compare(a, b);
        }
    }
}