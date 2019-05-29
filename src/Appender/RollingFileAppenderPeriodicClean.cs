using System;
using System.IO;
using log4net.Util.TypeConverters;

namespace log4net.Appender
{
    /// <summary>
    /// Appender that keeps only the entries falling in the last specified period.
    /// </summary>
    /// <inheritdoc cref="RollingFileAppender"/>
    public class RollingFileAppenderPeriodicClean : RollingFileAppender
    {
        private static readonly DateTime ReferenceDate = new DateTime(1999, 1, 1, 1, 1, 1);

        /// <summary>
        /// Period to preserve.
        /// </summary>
        /// <remarks>
        /// Default value set to 100 years (365 days a year) for safety. 
        /// Setting <c>TimeSpan.MaxValue</c> results into unrepresentable 
        /// time on addition or subraction.
        /// </remarks>
        private TimeSpan m_rollWindow = TimeSpan.FromDays(36500);

        public string RollWindow
        {
            get { return m_rollWindow.ToString("c"); }
            set
            {
                m_rollWindow = (TimeSpan) ConverterRegistry.GetConvertFrom(typeof(TimeSpan))
                    .ConvertFrom(value);
            }
        }

        public TimeSpan CheckInterval { get; private set; }

        public DateTime NextRollSchedule { get; private set; }

        public RollingFileAppenderPeriodicClean()
        {
            if (ConverterRegistry.GetConvertTo(typeof(string), typeof(TimeSpan)) == null)
            {
                ConverterRegistry.AddConverter(typeof(TimeSpan), typeof(StringToTimespanConverter));
            }
        }

        protected override void AdjustFileBeforeAppend()
        {
            base.AdjustFileBeforeAppend();

            if (DateTimeStrategy.Now >= NextRollSchedule)
            {
                DeleteOldFiles();
                UpdateNextRollSchedule();
            }
        }

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            DeleteOldFiles();
            ComputeCheckInterval();
            UpdateNextRollSchedule();
        }

        /// <summary>
        /// Computes the interval between checks for roll: <see cref="CheckInterval"/>.
        /// </summary>
        /// <remarks>
        /// <para>Computed only once, during activation.</para>
        /// <para>Check interval is the smallest non-zero component in 
        /// <see cref="RollingFileAppender.DatePattern"/>.</para>
        /// </remarks>
        protected void ComputeCheckInterval()
        {
            var referenceDateString = ReferenceDate.ToString(DatePattern, 
                System.Globalization.DateTimeFormatInfo.InvariantInfo);

            var referenceDateConvertedBack = DateTime.ParseExact(referenceDateString, DatePattern,
                System.Globalization.DateTimeFormatInfo.InvariantInfo);

            if (referenceDateConvertedBack.Second > 0)
            {
                CheckInterval = TimeSpan.FromSeconds(1);
                return;
            }

            if (referenceDateConvertedBack.Minute > 0)
            {
                CheckInterval = TimeSpan.FromMinutes(1);
                return;
            }

            if (referenceDateConvertedBack.Hour > 0)
            {
                CheckInterval = TimeSpan.FromHours(1);
                return;
            }

            if (referenceDateConvertedBack.Day > 0)
            {
                CheckInterval = TimeSpan.FromDays(1);
            }
        }

        protected void UpdateNextRollSchedule()
        {
            var currentTimeConvertedBack = NormalizeTime(DateTimeStrategy.Now);

            NextRollSchedule = currentTimeConvertedBack.Add(CheckInterval);
        }

        /// <summary>
        /// Deletes files older than the specified window.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All files matching the pattern <i>*file_name*</i> are listed; their last modified 
        /// time is extracted and normalized with <see cref="RollingFileAppender.DatePattern"/>; 
        /// all files with  last modified date older than the window are deleted.
        /// </para>
        /// <para>Only the root log directory is processed; no recursive folder processing.</para>
        /// </remarks>
        protected void DeleteOldFiles()
        {
            var cutOffDate = NormalizeTime(DateTimeStrategy.Now).Subtract(m_rollWindow);

            using (SecurityContext.Impersonate(this))
            {
                var logFileNamePattern = $"*{Path.GetFileNameWithoutExtension(File)}*";
                var logFiles = Directory.GetFiles(Path.GetDirectoryName(File), logFileNamePattern);

                foreach (var file in logFiles)
                {
                    var lastWrite = NormalizeTime(System.IO.File.GetLastWriteTime(file));

                    if (lastWrite < cutOffDate)
                    {
                        DeleteFile(file);
                    }
                }
            }
        }

        /// <summary>
        /// Normalizes a <see cref="DateTime"/> instance up to the precision specified 
        /// by <see cref="RollingFileAppender.DatePattern"/>.
        /// </summary>
        /// <param name="time">The <see cref="DateTime"/> instance to be normalized.</param>
        /// <returns>Normalized <see cref="DateTime"/> instance.</returns>
        /// <example>
        /// </example>
        protected DateTime NormalizeTime(DateTime time)
        {
            var currentTimeString = time.ToString(DatePattern, 
                System.Globalization.DateTimeFormatInfo.InvariantInfo);

            var currentTimeConvertedBack = DateTime.ParseExact(currentTimeString, DatePattern,
                System.Globalization.DateTimeFormatInfo.InvariantInfo);

            return currentTimeConvertedBack;
        }
    }

    public class StringToTimespanConverter : IConvertFrom
    {
        //public bool CanConvertTo(Type targetType)
        //{
        //    return targetType == typeof (TimeSpan);
        //}

        //public object ConvertTo(object source, Type targetType)
        //{
        //    var str = source as string;

        //    if (str == null)
        //    {
        //        throw new InvalidOperationException();
        //    }

        //    str = str.Trim();
        //    var x = str.IndexOf("D", StringComparison.InvariantCulture);

        //    if (x != -1)
        //    {
        //        var days = int.Parse(str.Substring(0, x).Trim(), 
        //            System.Globalization.CultureInfo.InvariantCulture);

        //        return TimeSpan.FromDays(days);
        //    }

        //    var time = TimeSpan.Parse(str, System.Globalization.CultureInfo.InvariantCulture);

        //    return time;
        //}

        public bool CanConvertFrom(Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public object ConvertFrom(object source)
        {
            var str = source as string;

            if (str == null)
            {
                throw new InvalidOperationException();
            }

            str = str.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture);
            var ind = str.IndexOf("d", StringComparison.InvariantCulture);

            if (ind != -1)
            {
                var days = int.Parse(str.Substring(0, ind).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);

                return TimeSpan.FromDays(days);
            }

            var time = TimeSpan.Parse(str, System.Globalization.CultureInfo.InvariantCulture);

            return time;
        }
    }
}
