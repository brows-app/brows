using Brows.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO;

/// <summary>
/// Defines a contract for file system operations, including reading, writing, deleting, and managing files and
/// directories. This interface provides asynchronous methods  to interact with the file system, supporting optional
/// arguments and cancellation tokens.
/// </summary>
/// <remarks>
/// Implementations of this interface are expected to handle file system operations in a platform-agnostic manner.
/// The methods support optional parameters for additional configuration and allows cancellation through
/// <see cref="CancellationToken"/>.
/// </remarks>
public interface IFileSys : IExport {
    /// <summary>
    /// Reads the contents of a file as a string.
    /// </summary>
    /// <param name="path">The path to the file to be read.</param>
    /// <param name="arg">Optional file system arguments that influence how the file is accessed. Can be null.</param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the file's contents as a string.
    /// </returns>
    Task<string> ReadFileText(
        string path,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Writes the specified text to a file at the given path, optionally using the provided file system arguments.
    /// </summary>
    /// <remarks>If the file at the specified <paramref name="path"/> already exists, it will be overwritten.</remarks>
    /// <param name="path">The path of the file to write to.</param>
    /// <param name="contents">The text content to write to the file.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteFileText(
        string path,
        string contents,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Deletes the specified file from the file system.
    /// </summary>
    /// <param name="path">The path of the file to delete.</param>
    /// <param name="arg">Optional argument providing additional file system options or context. Can be null.</param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    Task DeleteFile(
        string path,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Deletes the specified directory from the file system.
    /// </summary>
    /// <remarks>
    /// If <paramref name="recursive"/> is <see langword="true"/>, the method will attempt to delete
    /// all  subdirectories and files within the specified directory. Use caution when enabling this option, as it may
    /// result in the deletion of a large number of files and directories.
    /// </remarks>
    /// <param name="path">The path of the directory to delete.</param>
    /// <param name="recursive">
    /// A value indicating whether to delete the directory's contents recursively.
    /// If <see langword="true"/>, all subdirectories and files within the directory are deleted. 
    /// If <see langword="false"/>, the directory must be empty to be deleted.
    /// </param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A <see cref="CancellationToken"/> to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    Task DeleteDirectory(
        string path,
        bool recursive = false,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <remarks>
    /// If the directory already exists, this method does nothing. The operation is performed
    /// asynchronously.
    /// </remarks>
    /// <param name="path">The full path where the directory should be created.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<DirectoryInfo> CreateDirectory(
        string path,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves the list of file paths from the specified directory.
    /// </summary>
    /// <param name="path">The path of the directory to retrieve file paths from.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an array of file paths in the
    /// specified directory. If the directory is empty, the array will be empty.
    /// </returns>
    Task<string[]> GetDirectoryFiles(
        string path,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves the list of file paths from the specified directory.
    /// </summary>
    /// <param name="path">The path of the directory to retrieve file paths from.</param>
    /// <param name="searchPattern">The search pattern.</param>
    /// <param name="searchOption">The search option.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an array of file paths in the
    /// specified directory. If the directory is empty, the array will be empty.
    /// </returns>
    Task<string[]> GetDirectoryFiles(
        string path,
        string searchPattern,
        SearchOption searchOption,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves the list of directory paths from the specified directory.
    /// </summary>
    /// <param name="path">The path of the directory to retrieve directory paths from.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an array of directory paths in the
    /// specified directory. If the directory is empty, the array will be empty.
    /// </returns>
    Task<string[]> GetDirectoryDirectories(
        string path,
        FileSysArg arg = null,
        CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves the list of directory paths from the specified directory.
    /// </summary>
    /// <param name="path">The path of the directory to retrieve directory paths from.</param>
    /// <param name="searchPattern">The search pattern.</param>
    /// <param name="searchOption">The search option.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an array of directory paths in the
    /// specified directory. If the directory is empty, the array will be empty.
    /// </returns>
    Task<string[]> GetDirectoryDirectories(string path,
                                           string searchPattern,
                                           SearchOption searchOption,
                                           FileSysArg arg,
                                           CancellationToken token);

    /// <summary>
    /// Asynchronously opens a file for writing.
    /// </summary>
    /// <param name="path">The path of the file to open.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the file stream.
    /// </returns>
    Task<Stream> OpenWrite(string path, FileSysArg arg = null, CancellationToken token = default);

    /// <summary>
    /// Asynchronously opens a file for reading.
    /// </summary>
    /// <param name="path">The path of the file to open.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the file stream.
    /// </returns>
    Task<Stream> OpenRead(string path, FileSysArg arg = null, CancellationToken token = default);

    /// <summary>
    /// Asynchronously determines whether or not a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to check for existence.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see langword="true"/>
    /// if the file exists at the specified path; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> FileExists(string path, FileSysArg arg = null, CancellationToken token = default);

    /// <summary>
    /// Asynchronously determines whether or not a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the directory to check for existence.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see langword="true"/>
    /// if the directory exists at the specified path; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> DirectoryExists(string path, FileSysArg arg = null, CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves file information for the file at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to retrieve information for.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="System.IO.FileInfo"/>
    /// instance for the specified path.
    /// </returns>
    Task<FileInfo> FileInfo(string path, FileSysArg arg = null, CancellationToken token = default);

    /// <summary>
    /// Asynchronously retrieves directory information for the directory at the specified path.
    /// </summary>
    /// <param name="path">The path of the directory to retrieve information for.</param>
    /// <param name="arg">
    /// Optional argument that specifies additional file system options or filters.
    /// If null, default options are used.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="System.IO.DirectoryInfo"/>
    /// instance for the specified path.
    /// </returns>
    Task<DirectoryInfo> DirectoryInfo(string path, FileSysArg arg = null, CancellationToken token = default);
}
