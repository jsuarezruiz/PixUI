namespace PixUI.Demo.Mac
{
    public sealed class DemoDataGrid : View
    {
        private readonly DataGridController<Person> _controller;

        public DemoDataGrid()
        {
            _controller = new DataGridController<Person>();
            _controller.DataSource = Person.GeneratePersons(1000);

            var redCellStyle = new CellStyle { Color = Colors.Red };
            var greenCellStyle = new CellStyle { Color = Colors.Green };

            Child = new Container
            {
                Padding = EdgeInsets.All(20),
                Child = new DataGrid<Person>(_controller)
                {
                    Columns =
                    {
                        new DataGridRowNumColumn<Person>("行号") { Width = 60, Frozen = true },
                        new DataGridTextColumn<Person>("Name", p => p.Name) { Width = 500 },
                        new DataGridGroupColumn<Person>("Gender")
                        {
                            new DataGridIconColumn<Person>("Icon",
                                p => p.Female ? MaterialIcons.Woman : MaterialIcons.Man, 60)
                            {
                                CellStyleGetter = (p, _) => p.Female ? greenCellStyle : redCellStyle
                            },
                            new DataGridCheckboxColumn<Person>("IsMan", p => !p.Female) { Width = 100 },
                        },
                        new DataGridTextColumn<Person>("Score", p => p.Score.ToString()) { Width = 100 },
                        new DataGridButtonColumn<Person>("Action",
                            (person, _) => new Button(icon: MaterialIcons.ShoppingCart)
                            {
                                OnTap = _ => Notification.Info(person.Name)
                            },
                            80) { Frozen = true },
                    }
                }
            };
        }
    }
}