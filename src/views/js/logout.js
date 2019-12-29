document.addEventListener("DOMContentLoaded", () => {
  const header = "STI-Deauthorization"
  const form = document.querySelector(`form`)

  const query = async () => {
    try {
      const params = { method: "POST", headers: { [header]: "" } }
      return await (await fetch("/", params)).json()
    } catch (e) {
      alert(e.message)
    }
  }

  form.onsubmit = async (e) => {
    e.preventDefault()
    const { ok } = await query()
    if (!ok) {
      alert(`🙅‍♀️🔐`)
    } else {
      location.reload()
    }
  }
})
