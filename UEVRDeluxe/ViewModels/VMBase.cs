using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UEVRDeluxe.ViewModels;

/// <summary>
/// Implementation of <see cref="INotifyPropertyChanged"/> to simplify models.
/// </summary>
public abstract class VMBase : INotifyPropertyChanged {
    /// <summary> Occurs when a property value changes.</summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Notifies listeners that a property value has changed.
    /// </summary>
    /// <param name="propertyName">Name of the property used to notify listeners. This
    /// value is optional and can be provided automatically when invoked from compilers
    /// that support <see cref="CallerMemberNameAttribute"/>.</param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Checks if a property already matches a desired value. Sets the property and
    /// notifies listeners only when necessary.
    /// </summary>
    /// <typeparam name="T">Type of the property.</typeparam>
    /// <param name="storage">Reference to a property with both getter and setter.</param>
    /// <param name="value">Desired value for the property.</param>
    /// <param name="propertyName">Name of the property used to notify listeners. This
    /// value is optional and can be provided automatically when invoked from compilers that
    /// support CallerMemberName.</param>
    /// <param name="additionalProperty">Zusätzliches Property zu benachrichtigen.</param>
    /// <param name="additionalProperty2">Zusätzliches Property zu benachrichtigen.</param>
    /// <returns>True if the value was changed, false if the existing value matched the
    /// desired value.</returns>
    protected bool Set<T>(ref T storage, T value, string[] additionalProperties = null, [CallerMemberName] string propertyName = null) {
        if (Equals(storage, value)) return false;

        storage = value;
        OnPropertyChanged(propertyName);
        if (additionalProperties != null) {
            foreach (string prop in additionalProperties)
                if (!string.IsNullOrEmpty(prop)) OnPropertyChanged(prop);
        }

        return true;
    }

    bool isLoading;
    /// <summary>On many pages is a loading control</summary>
    public bool IsLoading { get => isLoading; set => Set(ref isLoading, value, [nameof(VisibleIfLoading)]); }
	public Visibility VisibleIfLoading => IsLoading ? Visibility.Visible : Visibility.Collapsed;
}
