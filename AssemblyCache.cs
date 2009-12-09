#region Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>
/// <copyright>
/// Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in
/// all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
/// THE SOFTWARE.
/// </copyright>
#endregion

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Mono.Cecil;

namespace Obfuscar
{
	class AssemblyCache
	{
		private readonly Project project;

		private readonly Dictionary<string, AssemblyDefinition> cache =
			new Dictionary<string, AssemblyDefinition> ();
		private readonly IAssemblyResolver resolver = new DefaultAssemblyResolver ();

		private List<string> extraFolders = new List<string> ();

		public List<string> ExtraFolders { get { return extraFolders; } set { extraFolders = value; } }

		public AssemblyCache (Project project)
		{
			this.project = project;
		}

		private AssemblyDefinition SelfResolve (AssemblyNameReference name)
		{
			AssemblyDefinition assmDef;
			if(!cache.TryGetValue (name.FullName, out assmDef)) {
				assmDef = null;

				string [] exts = new string [] { ".dll", ".exe" };

				foreach(string ext in exts) {
					string file = Path.Combine (project.Settings.InPath, name.Name + ext);
					if(File.Exists (file) && MatchAssemblyName (file, name)) {
						assmDef = AssemblyFactory.GetAssembly (file);
						if(assmDef.Name.FullName != name.FullName) {
							assmDef = null;
							continue;
						}
						cache [name.FullName] = assmDef;
						break;
					}
				}
				if(assmDef == null) {
					foreach(string extrapath in extraFolders) {
						foreach(string ext in exts) {
							string file = Path.Combine (extrapath, name.Name + ext);
							if(File.Exists (file) && MatchAssemblyName (file, name)) {
								assmDef = AssemblyFactory.GetAssembly (file);
								if(assmDef.Name.FullName != name.FullName) {
									assmDef = null;
									continue;
								}
								cache [name.FullName] = assmDef;
								return assmDef;
							}
						}
					}
				}
			}

			return assmDef;
		}

		private bool MatchAssemblyName (string file, AssemblyNameReference name)
		{
			try {
				System.Reflection.AssemblyName an = System.Reflection.AssemblyName.GetAssemblyName (file);
				return (an.FullName == name.FullName);

			}
			catch {
				return true;
			}
		}

		public TypeDefinition GetTypeDefinition (TypeReference type)
		{
			if(type == null)
				return null;

			TypeDefinition typeDef = type as TypeDefinition;
			if(typeDef == null) {
				AssemblyNameReference name = type.Scope as AssemblyNameReference;
                if (name != null)
				{
					// try to self resolve, fall back to default resolver
					AssemblyDefinition assmDef = null;
                    assmDef = SelfResolve(name);
                    if (assmDef == null)
					{
						try
						{
                            assmDef = resolver.Resolve(name);
							cache[name.FullName] = assmDef;
						}
                        catch (FileNotFoundException)
						{
                            throw new ApplicationException("Unable to resolve dependency:  " + name.Name);
						}
					}

                    string fullName = null;
                    while (type.IsNested)
                    {
                        if (fullName == null)
                            fullName = type.Name;
                        else
                            fullName = type.Name + "/" + fullName;
                        type = type.DeclaringType;
                    }
                    if (fullName == null)
                        fullName = type.Namespace + "." + type.Name;
                    else
                        fullName = type.Namespace + "." + type.Name + "/" + fullName;
                    typeDef = assmDef.MainModule.Types[fullName];
				}
				else
				{
					GenericInstanceType gi = type as GenericInstanceType;
					if (gi != null)
						return GetTypeDefinition(gi.ElementType);
				}
                
			}

			return typeDef;
		}
	}
}
