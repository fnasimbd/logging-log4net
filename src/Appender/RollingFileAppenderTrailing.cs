using System;
using System.IO;
using log4net.Util.TypeConverters;

namespace log4net.Appender
{
    /// <summary>
    /// Appender that keeps only the entries trailing the current time by a specified period 
    /// (e. g. last 3 hours, last 5 days, etc.)
    /// </summary>
    /// <inheritdoc cref="RollingFileAppender"/>
    public class RollingFileAppenderTrailing : RollingFileAppender
    {
	    /// <summary>
        /// Period to be preserved.
        /// </summary>
        /// <remarks>
        /// Default value set to 100 years (365 days a year) for safety. 
        /// Setting <c>TimeSpan.MaxValue</c> results into unrepresentable 
        /// time on addition or subraction.
        /// </remarks>
        private TimeSpan m_trailPeriod = TimeSpan.FromDays(36500);

        /// <summary>
        /// Period to be preserved.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Accepts standard .NET <see cref="TimeSpan"/> string representations.
        /// </para>
        /// </remarks>
        public string TrailPeriod
        {
            get { return m_trailPeriod.ToString("c"); }
            set
            {
                m_trailPeriod = (TimeSpan) ConverterRegistry.GetConvertFrom(typeof(TimeSpan))
                    .ConvertFrom(value);
            }
        }

        public TimeSpan CleanupCheckInterval { get; private set; }

        public DateTime NextCleanupSchedule { get; private set; }

        public RollingFileAppenderTrailing()
        {
            if (ConverterRegistry.GetConvertTo(typeof(string), typeof(TimeSpan)) == null)
            {
                ConverterRegistry.AddConverter(typeof(TimeSpan), typeof(StringToTimespanConverter));
            }
        }

		/// <summary>
		/// Cleans up old files if cleanup schedule is overdue.
		/// </summary>
        protected override void AdjustFileBeforeAppend()
        {
            base.AdjustFileBeforeAppend();

            if (DateTimeStrategy.Now >= NextCleanupSchedule)
            {
                DeleteOldFiles();
                UpdateNextRollSchedule();
            }
        }

        /// <summary>
        /// Initialize the appender based on the options set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deletes outdated files, if there are any, computes file rolling check 
        /// interval from <see cref="RollingFileAppender.DatePattern"/>, and sets the 
        /// next rolling schedule.
        /// </para>
        /// </remarks>
        public override void ActivateOptions()
        {
            base.ActivateOptions();

            ComputeCleanupCheckInterval();
	        CheckFileRollingStyleCompatibility();
            DeleteOldFiles();
            UpdateNextRollSchedule();
        }

	    /// <summary>
        /// Computes the interval between checks for roll: <see cref="CleanupCheckInterval"/>.
        /// </summary>
        /// <remarks>
        /// <para>Computed only once, during activation.</para>
        /// <para>Check interval is the smallest non-zero component in 
        /// <see cref="m_trailPeriod"/>.</para>
        /// </remarks>
        protected void ComputeCleanupCheckInterval()
        {
            if (m_trailPeriod.Seconds > 0)
            {
                CleanupCheckInterval = TimeSpan.FromSeconds(1);
                return;
            }

            if (m_trailPeriod.Minutes > 0)
            {
                CleanupCheckInterval = TimeSpan.FromMinutes(1);
                return;
            }

            if (m_trailPeriod.Hours > 0)
            {
                CleanupCheckInterval = TimeSpan.FromHours(1);
                return;
            }

            if (m_trailPeriod.Days > 0)
            {
                CleanupCheckInterval = TimeSpan.FromDays(1);
            }
        }

		/// <summary>
		/// File rolling frequency.
		/// </summary>
		/// <remarks>
		/// File rolling frequency must be smaller than the <see cref="TrailPeriod"/>.
		/// </remarks>
		protected void CheckFileRollingStyleCompatibility()
		{
			if (RollingStyle == RollingMode.Date && CleanupCheckInterval > m_trailPeriod)
			{
				// todo: error!
			}
		}

        protected void UpdateNextRollSchedule()
        {
            var currentTimeNormalized = NormalizeTime(DateTimeStrategy.Now);

            NextCleanupSchedule = currentTimeNormalized.Add(CleanupCheckInterval);
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
            var cutOffDate = NormalizeTime(DateTimeStrategy.Now).Subtract(m_trailPeriod);

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
	        if (CleanupCheckInterval.Seconds > 0)
			{
				return time;
			}

			if (CleanupCheckInterval.Minutes > 0)
			{
				return time.Subtract(TimeSpan.FromSeconds(time.Second));
			}

			if (CleanupCheckInterval.Hours > 0)
			{
				return time.Subtract(TimeSpan.FromMinutes(time.Minute)).
					Subtract(TimeSpan.FromSeconds(time.Second));
			}

			if (CleanupCheckInterval.Days > 0)
			{
				return time.Subtract(TimeSpan.FromHours(time.Hour)).
					Subtract(TimeSpan.FromMinutes(time.Minute)).
					Subtract(TimeSpan.FromSeconds(time.Second));
			}

            return time.Subtract(TimeSpan.FromSeconds(5));
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
