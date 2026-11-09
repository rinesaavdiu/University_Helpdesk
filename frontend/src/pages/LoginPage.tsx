import { PageHeader } from '../components/PageHeader'
import { EmptyState } from '../components/StatusStates'

export function LoginPage() {
  return (
    <section className="page page--narrow">
      <PageHeader title="Sign in" description="Students and technicians sign in with their university email." />
      <EmptyState title="Sign-in form not built yet">
        <p>This screen is a placeholder until authentication is added.</p>
      </EmptyState>
    </section>
  )
}
