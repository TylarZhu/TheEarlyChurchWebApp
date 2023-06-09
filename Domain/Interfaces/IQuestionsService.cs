﻿using Domain.DBEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IQuestionsService
    {
        Task InitQuestions();
        Task<Questions?> RandomSelectAQuestion();
    }
}
