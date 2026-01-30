using System;
using System.Collections.Generic;
using Moq;
using SamplePlugin.MemoryReaders;
using AkadaemiaAnyder.Core.Models;
using Dalamud.Plugin.Services;

namespace AkadaemiaAnyder.Tests.Unit;

/// <summary>
/// Unit tests for memory reader components including SafeMemoryReader and PointerValidator.
/// </summary>
public class MemoryReaderTests
{
    #region SafeMemoryReader Tests

    [Fact]
    public void SafeMemoryReader_ReadData_ReturnsDataWhenSuccessful()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        var testData = new List<CraftingRecipe>
        {
            new CraftingRecipe { RecipeId = 1, ItemId = 100, ItemName = "Test Recipe", IsUnlocked = true }
        };
        mockInner.Setup(x => x.ReadData()).Returns(testData);

        var errorLogged = false;
        var warningLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => errorLogged = true,
            warning => warningLogged = true
        );

        // Act
        var result = safeReader.ReadData();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(100u, result[0].ItemId);
        Assert.False(errorLogged);
        Assert.False(warningLogged);
    }

    [Fact]
    public void SafeMemoryReader_ReadData_CatchesAccessViolationException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.ReadData()).Throws<AccessViolationException>();

        var errorLogged = false;
        var errorMessage = string.Empty;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => { errorLogged = true; errorMessage = error; },
            warning => { }
        );

        // Act
        var result = safeReader.ReadData();

        // Assert
        Assert.Null(result);
        Assert.True(errorLogged);
        Assert.Contains("Memory access violation", errorMessage);
    }

    [Fact]
    public void SafeMemoryReader_ReadData_CatchesNullReferenceException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.ReadData()).Throws<NullReferenceException>();

        var errorLogged = false;
        var errorMessage = string.Empty;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => { errorLogged = true; errorMessage = error; },
            warning => { }
        );

        // Act
        var result = safeReader.ReadData();

        // Assert
        Assert.Null(result);
        Assert.True(errorLogged);
        Assert.Contains("Null reference", errorMessage);
    }

    [Fact]
    public void SafeMemoryReader_ReadData_CatchesGenericException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.ReadData()).Throws(new InvalidOperationException("Test error"));

        var errorLogged = false;
        var errorMessage = string.Empty;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => { errorLogged = true; errorMessage = error; },
            warning => { }
        );

        // Act
        var result = safeReader.ReadData();

        // Assert
        Assert.Null(result);
        Assert.True(errorLogged);
        Assert.Contains("Unexpected read error", errorMessage);
    }

    [Fact]
    public void SafeMemoryReader_IsAvailable_ReturnsTrueWhenSuccessful()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.IsAvailable()).Returns(true);

        var errorLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => errorLogged = true,
            warning => { }
        );

        // Act
        var result = safeReader.IsAvailable();

        // Assert
        Assert.True(result);
        Assert.False(errorLogged);
    }

    [Fact]
    public void SafeMemoryReader_IsAvailable_ReturnsFalseOnException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.IsAvailable()).Throws<AccessViolationException>();

        var errorLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => errorLogged = true,
            warning => { }
        );

        // Act
        var result = safeReader.IsAvailable();

        // Assert
        Assert.False(result);
        Assert.True(errorLogged);
    }

    [Fact]
    public void SafeMemoryReader_GetTotalCount_ReturnsCountWhenSuccessful()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.GetTotalCount()).Returns(512);

        var errorLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => errorLogged = true,
            warning => { }
        );

        // Act
        var result = safeReader.GetTotalCount();

        // Assert
        Assert.Equal(512, result);
        Assert.False(errorLogged);
    }

    [Fact]
    public void SafeMemoryReader_GetTotalCount_ReturnsZeroOnException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.GetTotalCount()).Throws<NullReferenceException>();

        var errorLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => errorLogged = true,
            warning => { }
        );

        // Act
        var result = safeReader.GetTotalCount();

        // Assert
        Assert.Equal(0, result);
        Assert.True(errorLogged);
    }

    [Fact]
    public void SafeMemoryReader_GetUnlockedCount_ReturnsCountWhenSuccessful()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.GetUnlockedCount()).Returns(42);

        var warningLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => { },
            warning => warningLogged = true
        );

        // Act
        var result = safeReader.GetUnlockedCount();

        // Assert
        Assert.Equal(42, result);
        Assert.False(warningLogged);
    }

    [Fact]
    public void SafeMemoryReader_GetUnlockedCount_ReturnsZeroOnException()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();
        mockInner.Setup(x => x.GetUnlockedCount()).Throws(new InvalidOperationException());

        var warningLogged = false;
        var safeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            mockInner.Object,
            error => { },
            warning => warningLogged = true
        );

        // Act
        var result = safeReader.GetUnlockedCount();

        // Assert
        Assert.Equal(0, result);
        Assert.True(warningLogged);
    }

    [Fact]
    public void SafeMemoryReader_Constructor_ThrowsOnNullInner()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafeMemoryReader<List<CraftingRecipe>>(
                null!,
                error => { },
                warning => { }
            )
        );
    }

    [Fact]
    public void SafeMemoryReader_Constructor_ThrowsOnNullLogError()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafeMemoryReader<List<CraftingRecipe>>(
                mockInner.Object,
                null!,
                warning => { }
            )
        );
    }

    [Fact]
    public void SafeMemoryReader_Constructor_ThrowsOnNullLogWarning()
    {
        // Arrange
        var mockInner = new Mock<IMemoryReader<List<CraftingRecipe>>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafeMemoryReader<List<CraftingRecipe>>(
                mockInner.Object,
                error => { },
                null!
            )
        );
    }

    #endregion

    #region RecipeReader Tests

    [Fact]
    public void RecipeReader_IsAvailable_ReturnsFalseWhenUIStateNull()
    {
        // Arrange
        var mockLog = new Mock<IPluginLog>();
        var mockDataManager = new Mock<IDataManager>();
        var reader = new RecipeReader(mockLog.Object, mockDataManager.Object);

        // Act
        var result = reader.IsAvailable();

        // Assert - When not in game, UIState should be unavailable
        // This test validates graceful handling of null UIState
        Assert.False(result);
    }

    [Fact]
    public void RecipeReader_ReadData_ReturnsNullWhenNotAvailable()
    {
        // Arrange
        var mockLog = new Mock<IPluginLog>();
        var mockDataManager = new Mock<IDataManager>();
        var reader = new RecipeReader(mockLog.Object, mockDataManager.Object);

        // Act
        var result = reader.ReadData();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RecipeReader_GetTotalCount_ReturnsExpectedValue()
    {
        // Arrange
        var mockLog = new Mock<IPluginLog>();
        var mockDataManager = new Mock<IDataManager>();
        var reader = new RecipeReader(mockLog.Object, mockDataManager.Object);

        // Act
        var result = reader.GetTotalCount();

        // Assert - RecipeReader reports total count from game data
        Assert.Equal(512, result);
    }

    [Fact]
    public void RecipeReader_GetUnlockedCount_ReturnsZeroWhenNotAvailable()
    {
        // Arrange
        var mockLog = new Mock<IPluginLog>();
        var mockDataManager = new Mock<IDataManager>();
        var reader = new RecipeReader(mockLog.Object, mockDataManager.Object);

        // Act
        var result = reader.GetUnlockedCount();

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region PointerValidator Tests

    [Fact]
    public void PointerValidator_IsValidPointer_ReturnsFalseForZero()
    {
        // Act
        var result = PointerValidator.IsValidPointer(IntPtr.Zero);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PointerValidator_IsValidPointer_ReturnsFalseForNegativeOne()
    {
        // Act
        var result = PointerValidator.IsValidPointer(new IntPtr(-1));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PointerValidator_IsValidPointer_ReturnsFalseForLowMemoryAddress()
    {
        // Act - Addresses below 0x10000 are typically invalid on Windows
        var result = PointerValidator.IsValidPointer(new IntPtr(0x1000));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PointerValidator_IsValidPointer_ReturnsTrueForValidAddress()
    {
        // Act - Typical user-mode address
        var result = PointerValidator.IsValidPointer(new IntPtr(0x7FF000000000));

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0xFFFF)]
    public void PointerValidator_IsValidPointer_EdgeCases(long address)
    {
        // Act
        var result = PointerValidator.IsValidPointer(new IntPtr(address));

        // Assert
        Assert.False(result);
    }

    #endregion
}
