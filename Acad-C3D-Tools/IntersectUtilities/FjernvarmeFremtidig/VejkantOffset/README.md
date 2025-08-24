# Vejkant Offset - Redesigned Pipeline

## Overview

This project has been completely redesigned to provide a real-time WPF visualization of intersection analysis during the AutoCAD jig operation. The new architecture focuses on:

1. **Real-time visualization** using WPF Canvas and Shapes
2. **Coordinate transformation** from AutoCAD world coordinates to display coordinates
3. **Dark mode styling** with centralized theme management
4. **Clean separation of concerns** between analysis, transformation, and visualization

## Architecture

### Core Components

```
VejkantOffset/
├── App/
│   ├── JigController.cs              # Main controller orchestrating the pipeline
│   ├── JigCallbacksAdapter.cs        # Adapter for jig callbacks
│   └── Contracts/
│       └── IOffsetAnalyzer.cs        # Interface contracts
├── Core/
│   ├── Models/
│   │   └── VejkantAnalysis.cs        # Analysis result model
│   ├── CoordinateTransformation/
│   │   └── DisplayCoordinateSystem.cs # Coordinate transformation logic
│   └── Analysis/
│       └── Spatial/
│           └── Segment2d.cs          # 2D segment representation
├── UI/
│   ├── Models/
│   │   └── IntersectionVisualizationModel.cs # Display data model
│   ├── ViewModels/
│   │   └── IntersectionVisualizationViewModel.cs # MVVM ViewModel
│   ├── Views/
│   │   ├── IntersectionVisualizationControl.xaml # Canvas visualization control
│   │   └── OffsetPaletteView.xaml    # Main view
│   └── Resources/
│       └── DarkTheme.xaml            # Centralized dark theme styles
└── Rendering/
    ├── Scene.cs                       # AutoCAD rendering scene
    ├── TransientPreviewRenderer.cs    # Transient graphics renderer
    └── VejkantSceneComposer.cs       # Scene composition logic
```

## Key Features

### 1. Real-time Visualization
- **Fixed horizontal working line** that stays the same length during jig operation
- **Dynamic yellow intersection segments** showing where the working line intersects existing geometry
- **Distance measurement lines** from intersection points to the working line
- **Grid system** for reference and scale

### 2. Coordinate Transformation
- **DisplayCoordinateSystem** handles conversion from AutoCAD world coordinates to display coordinates
- **Fixed display dimensions** (800x400 pixels) with proper scaling
- **Perpendicular distance calculations** from any point to the working line
- **Side detection** (above/below working line)

### 3. Dark Mode Styling
- **Centralized theme** in `DarkTheme.xaml`
- **Consistent color palette** across all UI elements
- **Professional appearance** suitable for engineering applications

### 4. Performance Optimized
- **Canvas-based rendering** for efficient real-time updates
- **ItemsControl binding** for dynamic content
- **Minimal memory allocation** during updates

## Data Flow

```
AutoCAD Jig → JigController → Analyzer → VejkantAnalysis
                                    ↓
                            VejkantInspectorMapper
                                    ↓
                    IntersectionVisualizationModel
                                    ↓
                            WPF Canvas Visualization
```

## Usage

### 1. Basic Setup
```csharp
// Create the controller with all dependencies
var controller = new JigController(
    analyzer,
    renderer,
    visualizer,
    sceneComposer,
    inspectorMapper
);

// Run the jig
controller.Run(keywords, settings);
```

### 2. Customization
- **Display dimensions**: Modify constants in `DisplayCoordinateSystem.cs`
- **Colors**: Update `DarkTheme.xaml` for different color schemes
- **Grid spacing**: Adjust grid generation in `VejkantInspectorMapper.cs`

### 3. Testing
Use the test methods in `DisplayCoordinateSystemTests.cs` to verify coordinate transformation:
```csharp
DisplayCoordinateSystemTests.TestHorizontalLine();
DisplayCoordinateSystemTests.TestDiagonalLine();
```

## Technical Details

### Coordinate System
- **World coordinates**: AutoCAD drawing units (meters)
- **Display coordinates**: Fixed pixel dimensions (800x400)
- **Scaling**: Maintains aspect ratio while fitting all geometry
- **Working line**: Always displayed horizontally at Y=200

### Performance Considerations
- **Real-time updates**: Every jig movement triggers full redraw
- **Canvas efficiency**: Uses ItemsControl for dynamic content
- **Memory management**: Minimal object creation during updates
- **Binding optimization**: Direct property binding for performance

### Styling System
- **Resource dictionaries**: Centralized theme management
- **Dynamic resources**: Consistent styling across all controls
- **Color palette**: Professional dark theme with accent colors
- **Responsive design**: Adapts to different control sizes

## Future Enhancements

1. **Zoom and pan** capabilities for the canvas
2. **Layer management** for different visualization elements
3. **Export functionality** for analysis results
4. **Customizable grid** spacing and appearance
5. **Animation effects** for smooth transitions

## Dependencies

- **AutoCAD API**: For geometry and database access
- **WPF**: For visualization and UI
- **CommunityToolkit.Mvvm**: For MVVM pattern implementation
- **.NET Framework**: For general functionality

## Notes

- The working line is always displayed horizontally regardless of its actual orientation in AutoCAD
- All distances are calculated in the original world coordinate system
- The visualization updates in real-time as the jig moves
- The dark theme provides excellent contrast for engineering applications
