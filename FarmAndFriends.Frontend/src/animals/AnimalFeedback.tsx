import { useAnimals } from './useAnimals'

export function AnimalFeedback() {
  const { feedback, pending, busy, recover } = useAnimals()
  return <>
    {feedback && <p role={feedback.type === 'error' ? 'alert' : 'status'}
      className={`rounded-xl p-3 text-sm ${feedback.type === 'success' ? 'bg-emerald-100 text-emerald-950' : 'bg-amber-100 text-amber-950'}`}>
      {feedback.message}
    </p>}
    {pending && <div role="status" className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
      <p>Existe uma ação aguardando confirmação. Você pode recuperar o resultado mesmo após fechar este painel.</p>
      <button type="button" disabled={busy} onClick={() => void recover()}
        className="mt-2 min-h-11 rounded-lg bg-amber-800 px-4 font-bold text-white disabled:opacity-50">
        {busy ? 'Recuperando…' : 'Recuperar resultado'}
      </button>
    </div>}
  </>
}
