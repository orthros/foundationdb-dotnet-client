#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace Doxense
{
	using Doxense.Diagnostics.Contracts;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using JetBrains.Annotations;

	internal static class ExceptionExtensions
	{
		private static readonly MethodInfo s_preserveStackTrace;
		private static readonly MethodInfo s_prepForRemoting;

		static ExceptionExtensions()
		{
			try
			{
				s_preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
				s_prepForRemoting = typeof(Exception).GetMethod("PrepForRemoting", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			catch { }
			Contract.Ensures(s_preserveStackTrace != null, "Exception.InternalPreserveStackTrace not found?");
			Contract.Ensures(s_prepForRemoting != null, "Exception.PrepForRemoting not found?");
		}

		/// <summary>D�termine s'il s'agit d'une erreur fatale (qu'il faudrait bouncer)</summary>
		/// <param name="self">Exception � tester</param>
		/// <returns>True s'il s'agit d'une ThreadAbortException, OutOfMemoryException ou StackOverflowException, ou une AggregateException qui contient une de ces erreurs</returns>
		[Pure]
		public static bool IsFatalError([CanBeNull] this Exception self)
		{
			return self is System.Threading.ThreadAbortException || self is OutOfMemoryException || self is StackOverflowException || (self is AggregateException && IsFatalError(self.InnerException));
		}

		/// <summary>Pr�serve la stacktrace lorsqu'on cr�e une exception, qui sera re-throw� plus haut</summary>
		/// <param name="self">Exception qui a �t� catch�e</param>
		/// <returns>La m�me exception, mais avec la StackTrace pr�serv�e</returns>
		[NotNull]
		public static Exception PreserveStackTrace([NotNull] this Exception self)
		{
			self = UnwrapIfAggregate(self);
			if (s_preserveStackTrace != null) s_preserveStackTrace.Invoke(self, null);
			return self;
		}

		/// <summary>Pr�serve la stacktrace lorsqu'on veut re-thrower une exception catch�e</summary>
		/// <param name="self">Exception qui a �t� catch�e</param>
		/// <returns>La m�me exception, mais avec la StackTrace pr�serv�e</returns>
		/// <remarks>Similaire � l'extension m�thode PrepareForRethrow pr�sente dans System.CoreEx.dll du Reactive Framework</remarks>
		[NotNull]
		public static Exception PrepForRemoting([NotNull] this Exception self)
		{
			//TODO: cette extensions m�thode est �galement pr�sente dans System.CoreEx.dll du Reactive Framework!
			// il faudra peut etre a terme rerouter vers cette version (si un jour Sioux ref�rence Rx directement...)
			self = UnwrapIfAggregate(self);
			if (s_prepForRemoting != null) s_prepForRemoting.Invoke(self, null);
			return self;
		}

		/// <summary>Retourne la premi�re exeception non-aggregate trouv�e dans l'arbre des InnerExceptions</summary>
		/// <param name="self">AggregateException racine</param>
		/// <returns>Premi�re exception dans l'arbre des InnerExceptions qui ne soit pas de type AggregateException</returns>
		[NotNull]
		public static Exception GetFirstConcreteException([NotNull] this AggregateException self)
		{
			// dans la majorit� des cas, on a une branche avec potentiellement plusieurs couches de AggEx mais une seule InnerException
			var e = self.GetBaseException();
			if (!(e is AggregateException)) return e;

			// Sinon c'est qu'on a un arbre a plusieures branches, qu'on va devoir parcourir...
			var list = new Queue<AggregateException>();
			list.Enqueue(self);
			while (list.Count > 0)
			{
				foreach (var e2 in list.Dequeue().InnerExceptions)
				{
					if (e2 == null) continue;
					if (!(e2 is AggregateException x)) return e2; // on a trouv� une exception concr�te !
					list.Enqueue(x);
				}
			}
			// uhoh ?
			return self;
		}

		/// <summary>Retourne la premi�re exception non-aggregate si c'est une AggregateException, ou l'exception elle m�me dans les autres cas</summary>
		/// <param name="self"></param>
		/// <returns></returns>
		[NotNull]
		public static Exception UnwrapIfAggregate([NotNull] this Exception self)
		{
			return self is AggregateException aggEx ? GetFirstConcreteException(aggEx) : self;
		}

		/// <summary>Rethrow la premi�re exception non-aggregate trouv�e, en jettant les autres s'il y en a</summary>
		/// <param name="self">AggregateException racine</param>
		[ContractAnnotation("self:null => null")]
		public static Exception Unwrap(this AggregateException self)
		{
			return self != null ? GetFirstConcreteException(self).PrepForRemoting() : null;
		}

		/// <summary>Unwrap generic exceptions like <see cref="AggregateException"/> or <see cref="TargetInvocationException"/> to return the inner exceptions</summary>
		[NotNull]
		public static Exception Unwrap([NotNull] this Exception self)
		{
			if (self is AggregateException aggEx) return GetFirstConcreteException(aggEx);
			if (self is TargetInvocationException tiEx) return tiEx.InnerException ?? self;
			//add other type of "container" exceptions as required
			return self;
		}
	}
}
