import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
import { studentTicketApi, technicianTicketApi } from './api/appApi'
import { StudentTicketApiProvider } from './api/StudentTicketApiContext'
import { TechnicianTicketApiProvider } from './api/TechnicianTicketApiContext'
import { router } from './router'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <StudentTicketApiProvider api={studentTicketApi}>
      <TechnicianTicketApiProvider api={technicianTicketApi}>
        <RouterProvider router={router} />
      </TechnicianTicketApiProvider>
    </StudentTicketApiProvider>
  </StrictMode>,
)
