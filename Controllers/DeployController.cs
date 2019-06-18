using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Diagnostics;

namespace VoteFrank.Controllers
{
    [Produces("application/json")]
    [Route("deploy")]
    public class DeployController : Controller
    {
        // curl -d '{"repository":{"full_name":"praeclarum/VoteFrank"}, "key2":"value2"}' -H "Content-Type: application/json" -X POST  "http://\$votefrank:rmme0pdGhuDsYtHDYrddZRc7kwD7fAlyZrDQw6790qTtLjRZn92sw3oz1bmr@localhost:5000/deploy?scmType=GitHub"

        [HttpPost]
        public string Post()
        {
            // if (body.repository.full_name != "praeclarum/VoteFrank")
            // {
            //     Response.StatusCode = 404;
            //     return "";
            // }
            Console.WriteLine("DEPLOY!!!");

            var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "pull --rebase";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();

            return $"OK {process.ExitCode}";
        }

        public class DeployBody
        {
            public Repository repository;
        }

        public class Repository
        {
            public string full_name;
        }
    }
}
