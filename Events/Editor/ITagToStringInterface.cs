/// <summary>
/// Use this interface to override <see cref="object.ToString"/> method for types that do not allow that (such as enums)
/// </summary>
public interface ITagToString {
    string TagToString(object tag);
}
