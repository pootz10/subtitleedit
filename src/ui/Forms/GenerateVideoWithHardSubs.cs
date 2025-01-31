﻿using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class GenerateVideoWithHardSubs : Form
    {
        private bool _abort;
        private Subtitle _assaSubtitle;
        private readonly VideoInfo _videoInfo;
        private readonly string _inputVideoFileName;
        private static readonly Regex FrameFinderRegex = new Regex(@"[Ff]rame=\s*\d+", RegexOptions.Compiled);
        private long _processedFrames;
        private long _startTicks;
        private long _totalFrames;
        private StringBuilder _log;
        public string VideoFileName { get; private set; }

        public GenerateVideoWithHardSubs(Subtitle assaSubtitle, string inputVideoFileName, VideoInfo videoInfo, int? fontSize)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            _videoInfo = videoInfo;
            Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Title;
            _assaSubtitle = assaSubtitle;
            _inputVideoFileName = inputVideoFileName;
            buttonOK.Text = LanguageSettings.Current.Watermark.Generate;
            labelPleaseWait.Text = LanguageSettings.Current.General.PleaseWait;
            labelFontSize.Text = LanguageSettings.Current.ExportPngXml.FontSize;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            labelAudioEnc.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Encoding;
            labelVideoEncoding.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Encoding;
            labelAudioBitRate.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.BitRate;
            labelAudioSampleRate.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.SampleRate;
            checkBoxMakeStereo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Stereo;
            labelCRF.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Crf;
            labelPreset.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Preset;
            labelTune.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TuneFor;
            progressBar1.Visible = false;
            labelPleaseWait.Visible = false;
            labelProgress.Text = string.Empty;
            labelFileName.Text = string.Empty;
            labelPass.Text = string.Empty;
            comboBoxVideoEncoding.SelectedIndex = 0;
            comboBoxPreset.SelectedIndex = 5;
            comboBoxCrf.SelectedIndex = 6;
            comboBoxAudioEnc.SelectedIndex = 0;
            comboBoxAudioSampleRate.SelectedIndex = 1;
            comboBoxTune.SelectedIndex = 0;
            comboBoxAudioBitRate.Text = "128k";
            checkBoxTargetFileSize_CheckedChanged(null, null);

            comboBoxPreset.Text = Configuration.Settings.Tools.GenVideoPreset;
            comboBoxVideoEncoding.Text = Configuration.Settings.Tools.GenVideoEncoding;
            comboBoxCrf.Text = Configuration.Settings.Tools.GenVideoCrf;
            comboBoxTune.Text = Configuration.Settings.Tools.GenVideoTune;
            comboBoxAudioEnc.Text = Configuration.Settings.Tools.GenVideoAudioEncoding;
            comboBoxAudioSampleRate.Text = Configuration.Settings.Tools.GenVideoAudioSampleRate;
            checkBoxMakeStereo.Checked = Configuration.Settings.Tools.GenVideoAudioForceStereo;
            checkBoxTargetFileSize.Checked = Configuration.Settings.Tools.GenVideoTargetFileSize;

            numericUpDownWidth.Value = _videoInfo.Width;
            numericUpDownHeight.Value = _videoInfo.Height;
            if (fontSize.HasValue)
            {
                if (fontSize.Value < numericUpDownFontSize.Minimum)
                {
                    numericUpDownFontSize.Value = numericUpDownFontSize.Minimum;
                }
                else if (fontSize.Value > numericUpDownFontSize.Maximum)
                {
                    numericUpDownFontSize.Value = numericUpDownFontSize.Maximum;
                }
                else
                {
                    numericUpDownFontSize.Value = fontSize.Value;
                }

                var left = Math.Max(labelResolution.Left + labelResolution.Width, labelFontSize.Left + labelFontSize.Width) + 5;
                numericUpDownFontSize.Left = left;
                numericUpDownWidth.Left = left;
                labelX.Left = numericUpDownWidth.Left + numericUpDownWidth.Width + 3;
                numericUpDownHeight.Left = labelX.Left + labelX.Width + 3;
                labelInfo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.InfoAssaOff;
            }
            else
            {
                numericUpDownFontSize.Visible = false;
                labelFontSize.Visible = false;
                labelInfo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.InfoAssaOn;
            }

            textBoxLog.Visible = false;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _abort = true;
            if (buttonOK.Enabled)
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (string.IsNullOrWhiteSpace(outLine.Data))
            {
                return;
            }

            _log.AppendLine(outLine.Data);

            var match = FrameFinderRegex.Match(outLine.Data);
            if (!match.Success)
            {
                return;
            }

            var arr = match.Value.Split('=');
            if (arr.Length != 2)
            {
                return;
            }

            if (long.TryParse(arr[1].Trim(), out var f))
            {
                _processedFrames = f;
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            buttonOK.Enabled = false;
            numericUpDownFontSize.Enabled = false;
            using (var saveDialog = new SaveFileDialog { FileName = string.Empty, Filter = "MP4|*.mp4|Matroska|*.mkv|WebM|*.webm" })
            {
                if (saveDialog.ShowDialog(this) != DialogResult.OK)
                {
                    buttonOK.Enabled = true;
                    numericUpDownFontSize.Enabled = true;
                    return;
                }

                VideoFileName = saveDialog.FileName;
            }

            _totalFrames = (long)_videoInfo.TotalFrames;

            _log = new StringBuilder();
            _log.AppendLine("Target file name: " + VideoFileName);
            _log.AppendLine("Video info width: " + _videoInfo.Width);
            _log.AppendLine("Video info width: " + _videoInfo.Height);
            _log.AppendLine("Video info total frames: " + _videoInfo.TotalFrames);
            _log.AppendLine("Video info total seconds: " + _videoInfo.TotalSeconds);

            if (File.Exists(VideoFileName))
            {
                File.Delete(VideoFileName);
            }

            labelFileName.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TargetFileName, VideoFileName);
            if (numericUpDownFontSize.Visible)
            {
                var fontSize = (int)numericUpDownFontSize.Value;
                var style = AdvancedSubStationAlpha.GetSsaStyle("Default", _assaSubtitle.Header);
                style.FontSize = fontSize;
                var styleLine = style.ToRawAss();
                _assaSubtitle.Header = AdvancedSubStationAlpha.AddTagToHeader("Style", styleLine, "[V4+ Styles]", _assaSubtitle.Header);
            }
            else
            {
                _assaSubtitle = new Subtitle(_assaSubtitle);
                if (numericUpDownWidth.Value != _videoInfo.Width || numericUpDownHeight.Value != _videoInfo.Height)
                {
                    var sourceWidth = _videoInfo.Width;
                    var sourceHeight = _videoInfo.Height;
                    var targetWidth = numericUpDownWidth.Value;
                    var targetHeight = numericUpDownHeight.Value;
                    var styles = AdvancedSubStationAlpha.GetSsaStylesFromHeader(_assaSubtitle.Header);
                    foreach (var style in styles)
                    {
                        style.MarginLeft = AssaResampler.Resample(sourceWidth, targetWidth, style.MarginLeft);
                        style.MarginRight = AssaResampler.Resample(sourceWidth, targetWidth, style.MarginRight);
                        style.MarginVertical = AssaResampler.Resample(sourceHeight, targetHeight, style.MarginVertical);

                        style.FontSize = AssaResampler.Resample(sourceHeight, targetHeight, style.FontSize);

                        style.OutlineWidth = (decimal)AssaResampler.Resample(sourceHeight, targetHeight, (float)style.OutlineWidth);
                        style.ShadowWidth = (decimal)AssaResampler.Resample(sourceHeight, targetHeight, (float)style.ShadowWidth);
                        style.Spacing = (decimal)AssaResampler.Resample(sourceWidth, targetWidth, (float)style.Spacing);
                    }

                    _assaSubtitle.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromAdvancedSubStationAlpha(_assaSubtitle.Header, styles);

                    _assaSubtitle.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResX", "PlayResX: " + targetWidth.ToString(CultureInfo.InvariantCulture), "[Script Info]", _assaSubtitle.Header);
                    _assaSubtitle.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResY", "PlayResY: " + targetHeight.ToString(CultureInfo.InvariantCulture), "[Script Info]", _assaSubtitle.Header);

                    foreach (var p in _assaSubtitle.Paragraphs)
                    {
                        p.Text = AssaResampler.ResampleOverrideTagsFont(sourceWidth, targetWidth, sourceHeight, targetHeight, p.Text);
                        p.Text = AssaResampler.ResampleOverrideTagsPosition(sourceWidth, targetWidth, sourceHeight, targetHeight, p.Text);
                        p.Text = AssaResampler.ResampleOverrideTagsDrawing(sourceWidth, targetWidth, sourceHeight, targetHeight, p.Text, null);
                    }
                }
            }

            if (Configuration.Settings.General.RightToLeftMode && LanguageAutoDetect.CouldBeRightToLeftLanguage(_assaSubtitle))
            {
                for (var index = 0; index < _assaSubtitle.Paragraphs.Count; index++)
                {
                    var paragraph = _assaSubtitle.Paragraphs[index];
                    if (LanguageAutoDetect.ContainsRightToLeftLetter(paragraph.Text))
                    {
                        paragraph.Text = Utilities.FixRtlViaUnicodeChars(paragraph.Text);
                    }
                }
            }

            var format = new AdvancedSubStationAlpha();
            var assaTempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ass");
            File.WriteAllText(assaTempFileName, format.ToText(_assaSubtitle, null));

            groupBoxSettings.Enabled = false;
            progressBar1.Maximum = (int)_videoInfo.TotalFrames;
            progressBar1.Visible = true;
            labelPleaseWait.Visible = true;

            if (checkBoxTargetFileSize.Checked)
            {
                RunTwoPassEncoding(assaTempFileName);
            }
            else
            {
                RunOnePassEncoding(assaTempFileName);
            }

            progressBar1.Visible = false;
            labelPleaseWait.Visible = false;
            timer1.Stop();
            labelProgress.Text = string.Empty;
            groupBoxSettings.Enabled = true;

            try
            {
                File.Delete(assaTempFileName);
            }
            catch
            {
                // ignore
            }

            DialogResult = _abort ? DialogResult.Cancel : DialogResult.OK;
        }

        private void RunTwoPassEncoding(string assaTempFileName)
        {
            labelPass.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.PassX, "1");

            var audioMb = 0;
            if (comboBoxAudioEnc.Text == "copy")
            {
                audioMb = GetAudioFileSizeInMb();
            }

            // (MiB * 8192 [converts MiB to kBit]) / video seconds = kBit/s total bitrate
            var bitRate = (int)Math.Round(((double)numericUpDownTargetFileSize.Value - audioMb) * 8192.0 / _videoInfo.TotalSeconds);
            if (comboBoxAudioEnc.Text != "copy" && !string.IsNullOrWhiteSpace(comboBoxAudioBitRate.Text))
            {
                var audioBitRate = int.Parse(comboBoxAudioBitRate.Text.RemoveChar('k').TrimEnd());
                bitRate -= audioBitRate;
            }
            var videoBitRate = bitRate.ToString(CultureInfo.InvariantCulture) + "k";

            if (bitRate < 10)
            {
                MessageBox.Show($"Bitrate too low: {bitRate}k");
                return;
            }

            var process = GetFfmpegProcess(assaTempFileName, 1, videoBitRate);
            _log.AppendLine("ffmpeg arguments pass 1: " + process.StartInfo.Arguments);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
                if (_abort)
                {
                    process.Kill();
                    return;
                }

                var v = (int)_processedFrames;
                if (v >= progressBar1.Minimum && v <= progressBar1.Maximum)
                {
                    progressBar1.Value = v;
                }
            }


            labelPass.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.PassX, "2");
            process = GetFfmpegProcess(assaTempFileName, 2, videoBitRate);
            _log.AppendLine("ffmpeg arguments pass 2: " + process.StartInfo.Arguments);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
                if (_abort)
                {
                    process.Kill();
                    return;
                }

                var v = (int)_processedFrames;
                if (v >= progressBar1.Minimum && v <= progressBar1.Maximum)
                {
                    progressBar1.Value = v;
                }
            }
        }

        private int GetAudioFileSizeInMb()
        {
            var ffmpegLocation = Configuration.Settings.General.FFmpegLocation;
            if (!Configuration.IsRunningOnWindows && (string.IsNullOrEmpty(ffmpegLocation) || !File.Exists(ffmpegLocation)))
            {
                ffmpegLocation = "ffmpeg";
            }

            var tempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".aac");
            var process = new Process
            {
                StartInfo =
                {
                    FileName = ffmpegLocation,
                    Arguments = $"-i \"{_inputVideoFileName}\" -vn -acodec copy \"{tempFileName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            process.WaitForExit();
            try
            {
                var length = (int)Math.Round(new FileInfo(tempFileName).Length / 1024.0 / 1024);
                try
                {
                    File.Delete(tempFileName);
                }
                catch
                {
                    // ignore
                }

                return length;
            }
            catch
            {
                return 0;
            }
        }

        private void RunOnePassEncoding(string assaTempFileName)
        {
            var process = GetFfmpegProcess(assaTempFileName, null);
            _log.AppendLine("ffmpeg arguments: " + process.StartInfo.Arguments);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
                if (_abort)
                {
                    process.Kill();
                }

                var v = (int)_processedFrames;
                if (v >= progressBar1.Minimum && v <= progressBar1.Maximum)
                {
                    progressBar1.Value = v;
                }
            }
        }

        private Process GetFfmpegProcess(string assaTempFileName, int? passNumber = null, string twoPassBitRate = null)
        {
            var pass = string.Empty;
            if (passNumber.HasValue)
            {
                pass = passNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            return VideoPreviewGenerator.GenerateHardcodedVideoFile(
                _inputVideoFileName,
                assaTempFileName,
                VideoFileName,
                (int)numericUpDownWidth.Value,
                (int)numericUpDownHeight.Value,
                comboBoxVideoEncoding.Text,
                comboBoxPreset.Text,
                comboBoxCrf.Text,
                comboBoxAudioEnc.Text,
                checkBoxMakeStereo.Checked,
                comboBoxAudioSampleRate.Text.Replace("Hz", string.Empty).Trim(),
                comboBoxTune.Text,
                comboBoxAudioBitRate.Text,
                pass,
                twoPassBitRate,
                OutputHandler);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_processedFrames <= 0 || _videoInfo.TotalFrames <= 0)
            {
                return;
            }

            var durationMs = (DateTime.UtcNow.Ticks - _startTicks) / 10_000;
            var msPerFrame = (float)durationMs / _processedFrames;
            var estimatedTotalMs = msPerFrame * _totalFrames;
            var estimatedLeft = ToProgressTime(estimatedTotalMs - durationMs);
            labelProgress.Text = estimatedLeft;
        }

        public static string ToProgressTime(float estimatedTotalMs)
        {
            var timeCode = new TimeCode(estimatedTotalMs);
            if (timeCode.TotalSeconds < 60)
            {
                return string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TimeRemainingSeconds, (int)Math.Round(timeCode.TotalSeconds));
            }

            if (timeCode.TotalSeconds / 60 > 5)
            {
                return string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TimeRemainingMinutes, (int)Math.Round(timeCode.TotalSeconds / 60));
            }

            return string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TimeRemainingMinutesAndSeconds, timeCode.Minutes + timeCode.Hours * 60, timeCode.Seconds);
        }

        private void numericUpDownWidth_ValueChanged(object sender, EventArgs e)
        {
            var v = (int)numericUpDownWidth.Value;
            if (v % 2 == 1)
            {
                numericUpDownWidth.Value++;
            }
        }

        private void numericUpDownHeight_ValueChanged(object sender, EventArgs e)
        {
            var v = (int)numericUpDownHeight.Value;
            if (v % 2 == 1)
            {
                numericUpDownHeight.Value++;
            }
        }

        private void comboBoxAudioEnc_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkBoxMakeStereo.Enabled = comboBoxAudioEnc.Text != "copy";
            comboBoxAudioSampleRate.Enabled = comboBoxAudioEnc.Text != "copy";
            labelAudioSampleRate.Enabled = comboBoxAudioEnc.Text != "copy";
        }

        private void GenerateVideoWithHardSubs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                if (textBoxLog.Visible)
                {
                    textBoxLog.Visible = false;
                }
                else
                {
                    textBoxLog.Visible = true;
                    textBoxLog.ScrollBars = ScrollBars.Both;
                    textBoxLog.BringToFront();
                    textBoxLog.Dock = DockStyle.Fill;

                    if (_log == null)
                    {
                        var log = new StringBuilder();
                        log.AppendLine("Video info width: " + _videoInfo.Width);
                        log.AppendLine("Video info width: " + _videoInfo.Height);
                        log.AppendLine("Video info total frames: " + _videoInfo.TotalFrames);
                        log.AppendLine("Video info total seconds: " + _videoInfo.TotalSeconds);
                        log.AppendLine();
                        log.AppendLine("ffmpeg " + GetFfmpegProcess("input.ass", null).StartInfo.Arguments);
                        textBoxLog.Text = log.ToString();
                    }
                    else
                    {
                        textBoxLog.Text = _log.ToString();
                    }
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _log == null)
            {
                DialogResult = DialogResult.Cancel;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _log == null)
            {
                DialogResult = DialogResult.Cancel;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                linkLabelHelp_LinkClicked(null, null);
            }
        }

        private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (comboBoxVideoEncoding.Text == "libx265")
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/H.265");
            }
            else if (comboBoxVideoEncoding.Text == "libvpx-vp9")
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/VP9");
            }
            else
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/H.264");
            }
        }

        private void GenerateVideoWithHardSubs_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration.Settings.Tools.GenVideoEncoding = comboBoxVideoEncoding.Text;
            Configuration.Settings.Tools.GenVideoPreset = comboBoxPreset.Text;
            Configuration.Settings.Tools.GenVideoCrf = comboBoxCrf.Text;
            Configuration.Settings.Tools.GenVideoTune = comboBoxTune.Text;
            Configuration.Settings.Tools.GenVideoAudioEncoding = comboBoxAudioEnc.Text;
            Configuration.Settings.Tools.GenVideoAudioForceStereo = checkBoxMakeStereo.Checked;
            Configuration.Settings.Tools.GenVideoAudioSampleRate = comboBoxAudioSampleRate.Text;
            Configuration.Settings.Tools.GenVideoTargetFileSize = checkBoxTargetFileSize.Checked;
        }

        private void comboBoxVideoEncoding_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxCrf.Items.Clear();
            comboBoxCrf.BeginUpdate();
            comboBoxCrf.Items.Add(string.Empty);
            labelTune.Visible = true;
            comboBoxTune.Visible = true;
            if (comboBoxVideoEncoding.Text == "libx265")
            {
                for (int i = 0; i < 51; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "28";
            }
            else if (comboBoxVideoEncoding.Text == "libvpx-vp9")
            {
                for (int i = 4; i <= 63; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "10";
                labelTune.Visible = false;
                comboBoxTune.Visible = false;
                comboBoxTune.Text = string.Empty;
            }
            else
            {
                for (int i = 17; i <= 28; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "23";
            }
            comboBoxCrf.EndUpdate();
        }

        private void GenerateVideoWithHardSubs_Load(object sender, EventArgs e)
        {

        }

        private void GenerateVideoWithHardSubs_Shown(object sender, EventArgs e)
        {
            var targetFileSizeMb = (int)Math.Round(new FileInfo(_inputVideoFileName).Length / 1024.0 / 1024);
            numericUpDownTargetFileSize.Value = Math.Max(targetFileSizeMb, numericUpDownTargetFileSize.Minimum);
        }

        private void checkBoxTargetFileSize_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxTargetFileSize.Checked)
            {
                comboBoxAudioEnc.Text = "aac";
                labelAudioBitRate.Enabled = true;
                comboBoxAudioBitRate.Enabled = true;
            }
            else
            {
                labelAudioBitRate.Enabled = false;
                comboBoxAudioBitRate.Enabled = false;
            }
        }
    }
}
