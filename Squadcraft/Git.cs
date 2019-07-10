using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Squadcraft {
    public static class Git {
        public static void GitInit(DirectoryInfo dir, bool repair = false) {
            var dirs = dir.GetDirectories();
            var files = dir.GetFiles();
            if (dirs.Length + files.Length == 0) {
                //folder jest pusty
                Clone(dir);
            } else {
                bool contains = false;
                foreach (var tmpDir in dirs) {
                    if (tmpDir.Name == ".git") {
                        contains = true;
                        break;
                    }
                }
                if (contains == false) {
                    Repository.Init(dir.FullName);

                    using (Repository repo = new Repository(dir.FullName)) {
                        Remote remote = repo.Network.Remotes.Add("origin", Program.conf.gitRemotePath);
                    }
                } else {
                    if (repair) {
                        CommitAllChanges($"Update by {Program.conf.gitUsername} at {DateTime.Now.ToString()}");
                        PushCommits(Program.conf.gitRemotePath);
                        Database.Response dbResponse = Database.Response.unexpectedError;
                        string responseStr = Database.GetResponse(out dbResponse, true);
                        if (dbResponse == Database.Response.closedSuccessfully)
                            Console.WriteLine("Session closed successfully.");
                    } else {
                        Pull();
                    }
                }
            }
        }


        public static bool CommitAllChanges(string message) {
            using (var repo = new Repository(Program.conf.gitLocalDir.FullName)) {
                Commands.Stage(repo, "*");

                Signature author = new Signature(Program.conf.gitUsername, Program.conf.gitEmail, DateTime.Now);
                Signature committer = author;

                try {
                    Commit commit = repo.Commit(message, author, committer);
                    Console.WriteLine($"Commiting all changes...");
                    return true;
                } catch (EmptyCommitException) {
                    Console.WriteLine("Nothing to commit...");
                    return false;
                }
            }
        }

        public static void PushCommits(string remoteName) {
            Console.WriteLine($"Pushing GIT repository to {Program.conf.gitRemotePath}...");
            using (var repo = new Repository(Program.conf.gitLocalDir.FullName)) {
                Remote remote = repo.Network.Remotes["origin"];

                Branch branch = repo.Head;
                Branch updatedBranch = repo.Branches.Update(branch,
                    b => b.Remote = remote.Name,
                    b => b.UpstreamBranch = branch.CanonicalName);

                PushOptions options = new PushOptions();
                options.CredentialsProvider = new CredentialsHandler(
                    (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials() {
                            Username = Program.conf.gitUsername,
                            Password = Program.conf.gitPassword
                        });
                try {
                    repo.Network.Push(repo.Branches["master"], options);
                } catch (Exception e) {
                    Program.ErrorExit($"Could not push repo: {e.Message}");
                }
            }
        }

        public static void Pull() {
            Console.WriteLine($"Pulling GIT repository from {Program.conf.gitRemotePath}...");
            using (var repo = new Repository(Program.conf.gitLocalDir.FullName)) {
                PullOptions options = new PullOptions();
                options.FetchOptions = new FetchOptions();
                options.FetchOptions.CredentialsProvider = new CredentialsHandler(
                    (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials() {
                            Username = Program.conf.gitUsername,
                            Password = Program.conf.gitPassword
                        });

                var signature = new LibGit2Sharp.Signature(
                    new Identity(Program.conf.gitUsername, Program.conf.gitEmail), DateTimeOffset.Now);

                try {
                    Commands.Pull(repo, signature, options);
                } catch (Exception e) {
                    Program.ErrorExit($"Could not pull repo: {e.Message}");
                }
            }
        }

        public static void Clone(DirectoryInfo toDir) {
            Console.WriteLine($"Cloning GIT repository from {Program.conf.gitRemotePath}...");
            var co = new CloneOptions();
            co.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = Program.conf.gitUsername, Password = Program.conf.gitPassword };
            try {
                Repository.Clone(Program.conf.gitRemotePath, toDir.FullName, co);
            } catch (Exception e) {
                Program.ErrorExit($"Could not clone repo: {e.Message}");
            }
        }


    }
}