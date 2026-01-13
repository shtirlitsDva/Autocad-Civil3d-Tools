using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.DataScience.PropertySetBrowser
{
    public partial class PropertySetBrowserViewModel : ObservableObject
    {
        private readonly Database _database;
        private readonly ObservableCollection<PropertySetEntityRow> _allRows = new();
        private ICollectionView? _rowsView;

        [ObservableProperty]
        private ObservableCollection<string> propertySetNames = new();

        [ObservableProperty]
        private string? selectedPropertySetName;

        [ObservableProperty]
        private ObservableCollection<string> propertyColumns = new();

        /// <summary>
        /// Stores data types for each property column (for alignment purposes).
        /// </summary>
        public Dictionary<string, PsDataType> PropertyDataTypes { get; private set; } = new();

        /// <summary>
        /// The ICollectionView used for filtering - binds to DataGrid.
        /// Using ICollectionView.Filter is much faster than recreating ObservableCollections.
        /// </summary>
        public ICollectionView RowsView => _rowsView ??= CreateRowsView();

        /// <summary>
        /// Gets the count of filtered (visible) rows.
        /// </summary>
        public int FilteredCount => _rowsView?.Cast<object>().Count() ?? 0;

        /// <summary>
        /// Gets the total count of all rows.
        /// </summary>
        public int TotalCount => _allRows.Count;

        [ObservableProperty]
        private PropertySetEntityRow? selectedRow;

        [ObservableProperty]
        private string filterText = string.Empty;

        [ObservableProperty]
        private string statusText = "Select a PropertySet to view data.";

        [ObservableProperty]
        private bool isLoading;

        public PropertySetBrowserViewModel(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            LoadPropertySetNames();
        }

        private ICollectionView CreateRowsView()
        {
            var view = CollectionViewSource.GetDefaultView(_allRows);
            view.Filter = FilterPredicate;
            return view;
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not PropertySetEntityRow row)
                return false;
            return row.MatchesSearch(FilterText);
        }

        private void LoadPropertySetNames()
        {
            try
            {
                var doc = AcadApplication.DocumentManager.MdiActiveDocument;
                using (var docLock = doc.LockDocument())
                {
                    var names = PropertySetManager.GetPropertySetNames(_database);
                    PropertySetNames = new ObservableCollection<string>(names.OrderBy(x => x));
                }
            }
            catch (Exception ex)
            {
                prdDbg($"Error loading PropertySet names: {ex.Message}");
            }
        }

        partial void OnSelectedPropertySetNameChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                LoadPropertySetData(value);
            }
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void LoadPropertySetData(string propertySetName)
        {
            IsLoading = true;
            StatusText = $"Loading data for {propertySetName}...";
            _allRows.Clear();
            PropertyColumns.Clear();
            PropertyDataTypes.Clear();

            try
            {
                var doc = AcadApplication.DocumentManager.MdiActiveDocument;
                using (var docLock = doc.LockDocument())
                using (var tx = _database.TransactionManager.StartTransaction())
                {
                    // Get property definitions for columns
                    var propDefs = PropertySetManager.GetPropertyNamesAndDataTypes(_database, propertySetName);
                    PropertyColumns = new ObservableCollection<string>(propDefs.Keys.OrderBy(x => x));
                    PropertyDataTypes = new Dictionary<string, PsDataType>(propDefs);

                    // Get all entities with this PropertySet attached
                    var entities = _database.HashSetOfType<Entity>(tx, true)
                        .Where(e => PropertySetManager.IsPropertySetAttached(e, propertySetName))
                        .ToList();

                    foreach (var entity in entities)
                    {
                        var row = new PropertySetEntityRow
                        {
                            EntityHandle = entity.Handle,
                            EntityType = entity.GetType().Name
                        };

                        // Read all properties
                        foreach (var propName in propDefs.Keys)
                        {
                            if (PropertySetManager.TryReadNonDefinedPropertySetObject(
                                entity, propertySetName, propName, out object? result))
                            {
                                row.Properties[propName] = FormatPropertyValue(result, propDefs[propName]);
                            }
                            else
                            {
                                row.Properties[propName] = string.Empty;
                            }
                        }

                        _allRows.Add(row);
                    }

                    tx.Commit();
                }

                ApplyFilter();
                StatusText = $"Loaded {_allRows.Count} entities with PropertySet '{propertySetName}'.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading data: {ex.Message}";
                prdDbg($"Error loading PropertySet data: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string FormatPropertyValue(object? value, PsDataType dataType)
        {
            if (value == null)
                return string.Empty;

            // Handle special markers from TryReadNonDefinedPropertySetObject
            if (value is string strValue)
            {
                if (strValue == "<null>" || strValue == "<empty string>")
                    return string.Empty;
                return strValue;
            }

            return dataType switch
            {
                PsDataType.Real => value is double d ? d.ToString("G") : value.ToString() ?? string.Empty,
                PsDataType.Integer => value.ToString() ?? string.Empty,
                PsDataType.TrueFalse => value is bool b ? (b ? "Yes" : "No") : value.ToString() ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }

        private void ApplyFilter()
        {
            // Just refresh the view - the filter predicate will be re-evaluated
            // This is much faster than creating a new ObservableCollection
            RowsView.Refresh();
            
            StatusText = $"Showing {FilteredCount} of {TotalCount} entities.";
        }

        [RelayCommand]
        private void SelectEntity()
        {
            if (SelectedRow == null)
            {
                StatusText = "No row selected.";
                return;
            }

            SelectEntitiesByHandles(new[] { SelectedRow.EntityHandle });
        }

        [RelayCommand]
        private void SelectAllFiltered()
        {
            var filteredRows = RowsView.Cast<PropertySetEntityRow>().ToList();
            if (filteredRows.Count == 0)
            {
                StatusText = "No entities to select.";
                return;
            }

            var handles = filteredRows.Select(r => r.EntityHandle).ToArray();
            SelectEntitiesByHandles(handles);
        }

        [RelayCommand]
        private void ZoomToEntity()
        {
            if (SelectedRow == null)
            {
                StatusText = "No row selected.";
                return;
            }

            try
            {
                var doc = AcadApplication.DocumentManager.MdiActiveDocument;
                var editor = doc.Editor;

                using (var docLock = doc.LockDocument())
                using (var tx = _database.TransactionManager.StartTransaction())
                {
                    var entity = GetEntityByHandle(SelectedRow.EntityHandle, tx);
                    if (entity != null)
                    {
                        var extents = entity.GeometricExtents;
                        
                        // Add padding to the extents
                        var padding = Math.Max(
                            Math.Max(extents.MaxPoint.X - extents.MinPoint.X, 
                                     extents.MaxPoint.Y - extents.MinPoint.Y) * 0.2,
                            20); // At least 20 units padding
                        
                        var paddedExtents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(
                            new Autodesk.AutoCAD.Geometry.Point3d(
                                extents.MinPoint.X - padding,
                                extents.MinPoint.Y - padding,
                                extents.MinPoint.Z),
                            new Autodesk.AutoCAD.Geometry.Point3d(
                                extents.MaxPoint.X + padding,
                                extents.MaxPoint.Y + padding,
                                extents.MaxPoint.Z));
                        
                        // Use the existing Zoom extension method from UtilsCommon
                        editor.Zoom(paddedExtents);
                        
                        tx.Commit();
                        SelectEntitiesByHandles(new[] { SelectedRow.EntityHandle });
                        StatusText = $"Zoomed to entity {SelectedRow.EntityHandle}.";
                    }
                    else
                    {
                        tx.Commit();
                        StatusText = $"Entity {SelectedRow.EntityHandle} not found.";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error zooming: {ex.Message}";
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            if (!string.IsNullOrEmpty(SelectedPropertySetName))
            {
                LoadPropertySetData(SelectedPropertySetName);
            }
            else
            {
                LoadPropertySetNames();
                StatusText = "PropertySet list refreshed.";
            }
        }

        private void SelectEntitiesByHandles(Handle[] handles)
        {
            try
            {
                var doc = AcadApplication.DocumentManager.MdiActiveDocument;
                var editor = doc.Editor;

                using (var docLock = doc.LockDocument())
                using (var tx = _database.TransactionManager.StartTransaction())
                {
                    var ids = new List<ObjectId>();
                    foreach (var handle in handles)
                    {
                        var entity = GetEntityByHandle(handle, tx);
                        if (entity != null)
                            ids.Add(entity.Id);
                    }

                    tx.Commit();

                    if (ids.Count > 0)
                    {
                        editor.SetImpliedSelection(ids.ToArray());
                        StatusText = $"Selected {ids.Count} entities.";
                    }
                    else
                    {
                        StatusText = "No valid entities to select.";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error selecting entities: {ex.Message}";
            }
        }

        private Entity? GetEntityByHandle(Handle handle, Transaction tx)
        {
            try
            {
                var id = _database.GetObjectId(false, handle, 0);
                if (id == ObjectId.Null) return null;
                return tx.GetObject(id, OpenMode.ForRead) as Entity;
            }
            catch
            {
                return null;
            }
        }
    }
}
