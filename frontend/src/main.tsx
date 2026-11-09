import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
import { studentTicketApi } from './api/appApi'
import { StudentTicketApiProvider } from './api/StudentTicketApiContext'
import { router } from './router'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <StudentTicketApiProvider api={studentTicketApi}>
      <RouterProvider router={router} />
    </StudentTicketApiProvider>
  </StrictMode>,
)
