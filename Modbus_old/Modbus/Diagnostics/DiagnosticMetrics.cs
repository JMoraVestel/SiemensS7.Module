using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModbusModule.Diagnostics
{
    public class DiagnosticMetrics : INotifyPropertyChanged
    {
        /// <summary>
        /// Threshold (in ms) over which a read is considered overdue.
        /// </summary>
        protected double readOverdueThreshold = 1000;

        private int _tags;
        private int _totalReads;
        private int _failedReads;
        private int _totalWrites;
        private int _failedWrites;
        private int _failedConnectionAttempts;
        private bool _modbusMasterConnected;
        private double _avgOverdueTime;
        private double _avgWriteTime;
        private int _retries;
        private int _overdueReads;

        /// <summary>
        /// Maximum number of samples to use for rolling averages
        /// Default is 10 but can be overridden by derived classes
        /// </summary>
        protected virtual int DefaultRollingWindowSize => 10;

        public void ConnectionSuccess()
        {
            ModbusMasterConnected = true;
        }

        public void ConnectionFailed()
        {
            ModbusMasterConnected = false;
            FailedConnectionAttempts++;
        }

        /// <summary>
        /// Total number of tags
        /// </summary>
        public int TagsCount
        {
            get => _tags;
            protected set => SetProperty(ref _tags, value);
        }

        /// <summary>
        /// Total number of failed connection attempts
        /// </summary>
        public int FailedConnectionAttempts
        {
            get => _failedConnectionAttempts;
            protected set => SetProperty(ref _failedConnectionAttempts, value);
        }

        /// <summary>
        /// Modbus connection status.
        /// </summary>
        public bool ModbusMasterConnected
        {
            get => _modbusMasterConnected;
            private set => SetProperty(ref _modbusMasterConnected, value);
        }

        /// <summary>
        /// Total number of read operations performed
        /// </summary>
        public int TotalReads
        {
            get => _totalReads;
            set => SetProperty(ref _totalReads, value);
        }

        /// <summary>
        /// Total number of failed read operations
        /// </summary>
        public int FailedReads
        {
            get => _failedReads;
            set => SetProperty(ref _failedReads, value);
        }
        /// <summary>
        /// Total number of failed write operations
        /// </summary>
        public int FailedWrites
        {
            get => _failedWrites;
            set => SetProperty(ref _failedWrites, value);
        }

        /// <summary>
        /// Total number of write operations performed
        /// </summary>
        public int TotalWrites
        {
            get => _totalWrites;
            set => SetProperty(ref _totalWrites, value);
        }

        /// <summary>
        /// Number of retry attempts after failed operations
        /// </summary>
        public int Retries
        {
            get => _retries;
            set => SetProperty(ref _retries, value);
        }

        /// <summary>
        /// Number of read operations that exceeded the timeout threshold
        /// </summary>
        public int OverdueReads
        {
            get => _overdueReads;
            set => SetProperty(ref _overdueReads, value);
        }

        /// <summary>
        /// Average delay in milliseconds for read operations (rolling average of last N reads)
        /// </summary>
        public double AvgOverdueTime
        {
            get => _avgOverdueTime;
            set => SetProperty(ref _avgOverdueTime, value);
        }

        /// <summary>
        /// Average write time in milliseconds (rolling average of last N writes)
        /// </summary>
        public double AvgWriteTime
        {
            get => _avgWriteTime;
            set => SetProperty(ref _avgWriteTime, value);
        }

        /// <summary>
        /// Event triggered when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets property value and raises PropertyChanged event if value has changed
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="field">Reference to field backing the property</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Name of the property</param>
        /// <returns>True if value was changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public static object GetMetricValue(string metricName, DiagnosticMetrics metrics)
        {
            // Direct property access without reflection
            if (string.Equals(metricName, "TotalReads", StringComparison.OrdinalIgnoreCase))
                return metrics.TotalReads;
            else if (string.Equals(metricName, "TotalWrites", StringComparison.OrdinalIgnoreCase))
                return metrics.TotalWrites;
            else if (string.Equals(metricName, "FailedWrites", StringComparison.OrdinalIgnoreCase))
                return metrics.FailedWrites;
            else if (string.Equals(metricName, "FailedReads", StringComparison.OrdinalIgnoreCase))
                return metrics.FailedReads;
            else if (string.Equals(metricName, "Retries", StringComparison.OrdinalIgnoreCase))
                return metrics.Retries;
            else if (string.Equals(metricName, "OverdueReads", StringComparison.OrdinalIgnoreCase))
                return metrics.OverdueReads;
            else if (string.Equals(metricName, "AvgOverdueTime", StringComparison.OrdinalIgnoreCase))
                return metrics.AvgOverdueTime;
            else if (string.Equals(metricName, "AvgWriteTime", StringComparison.OrdinalIgnoreCase))
                return metrics.AvgWriteTime;
            else if (string.Equals(metricName, "FailedConnectionAttempts", StringComparison.OrdinalIgnoreCase))
                return metrics.FailedConnectionAttempts;
            else if (string.Equals(metricName, "ModbusMasterConnected", StringComparison.OrdinalIgnoreCase))
                return metrics.ModbusMasterConnected;
            else if (string.Equals(metricName, "TagsCount", StringComparison.OrdinalIgnoreCase))
                return metrics.TagsCount;

            return null!;
        }
    }
}
